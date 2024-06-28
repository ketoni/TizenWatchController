from datetime import datetime, timedelta
import logging
import math
import socket
from threading import Thread
import time
from typing import Optional

from matplotlib.patches import Rectangle
import matplotlib.pyplot as plt
from matplotlib.animation import FuncAnimation
from matplotlib.widgets import Slider
import numpy as np
from numpy.typing import NDArray
from pydantic import BaseModel
#from sklearn.metrics.pairwise import cosine_similarity
from scipy.signal import butter, lfilter

class Plotter:
    def __init__(self, source, window_size=100):
        self.source = source
        self.fig, self.ax = plt.subplots()
        self.x_window = window_size

        # Initialize data and plotting collections
        self.xdata = np.linspace(0, self.x_window, self.x_window)
        self.ydata = np.zeros((4, self.x_window))
        self.ln1, = self.ax.plot([], [], "r-", label="X", linewidth=0.5)
        self.ln2, = self.ax.plot([], [], "g-", label="Y", linewidth=0.5)
        self.ln3, = self.ax.plot([], [], "b-", label="Z", linewidth=0.5)
        self.ln4, = self.ax.plot([], [], "m-", label="cha", linewidth=1.5)

        # Initialize Butterworth filter coefficients for low-pass and high-pass filters
        self.low_cutoff = 0.8
        self.high_cutoff = 0.2
        self.update_filter_coefficients()

        # Create sliders for filter cutoff frequencies
        plt.subplots_adjust(left=0.15, bottom=0.25, right=0.95, top=0.95) # Room for sliders 
        axcolor = "lightgoldenrodyellow"
        ax_low_cutoff = plt.axes([0.2, 0.1, 0.65, 0.03], facecolor=axcolor)
        ax_high_cutoff = plt.axes([0.2, 0.15, 0.65, 0.03], facecolor=axcolor)
        self.slider_low_cutoff = Slider(ax_low_cutoff, "Low Cutoff", 0.01, 0.99, valinit=self.low_cutoff)
        self.slider_high_cutoff = Slider(ax_high_cutoff, "High Cutoff", 0.01, 0.99, valinit=self.high_cutoff)
        self.slider_low_cutoff.on_changed(self.update_cutoff)
        self.slider_high_cutoff.on_changed(self.update_cutoff)

        # Add an indicator rectangle
        self.indicator = Rectangle((0, -22), 1, 44, transform=self.ax.transAxes, color='none')
        self.ax.add_patch(self.indicator)

        self.ax.legend(loc='lower left')
        self.init()

    def init(self):
        self.ln1.set_data([], [])
        self.ln2.set_data([], [])
        self.ln3.set_data([], [])
        self.ln4.set_data([], [])
        self.ax.set_xlim(0, self.x_window)
        self.ax.set_ylim(-20.1, 20.1)
        return self.ln1, self.ln2, self.ln3, self.ln4

    def update_filter_coefficients(self):
        self.b_low, self.a_low = butter(3, self.low_cutoff, btype="low")
        self.b_high, self.a_high = butter(3, self.high_cutoff, btype="high")

    def butter_lowpass_filter(self, data: NDArray) -> NDArray:
        return lfilter(self.b_low, self.a_low, data)

    def butter_highpass_filter(self, data: NDArray) -> NDArray:
        return lfilter(self.b_high, self.a_high, data)

    def update_cutoff(self, val):
        self.low_cutoff = self.slider_low_cutoff.val
        self.high_cutoff = self.slider_high_cutoff.val
        self.update_filter_coefficients()

    def update(self, frame):
        t, x, y, z = frame

        # Update xdata and ydata
        self.xdata = np.append(self.xdata[1:], t)
        new_data = np.array([[x], [y], [z], [0.0]]) # Dummy
        self.ydata = np.hstack((self.ydata[:, 1:], new_data))

        # Apply high-pass filter to each axis and sum the results
        x_high = self.butter_highpass_filter(self.ydata[0])
        y_high = self.butter_highpass_filter(self.ydata[1])
        z_high = self.butter_highpass_filter(self.ydata[2])
        self.ydata[3, -1] = abs(x_high[-1]) + abs(y_high[-1]) + abs(z_high[-1])

        # Apply low-pass filter to smooth the deviation output and cap the value
        smoothed_value = self.butter_lowpass_filter(self.ydata[3])[-1]
        self.ydata[3, -1] = min(20, abs(smoothed_value))

        # Update the line data
        self.ln1.set_data(self.xdata, self.ydata[0])
        self.ln2.set_data(self.xdata, self.ydata[1])
        self.ln3.set_data(self.xdata, self.ydata[2])
        self.ln4.set_data(self.xdata, self.ydata[3])

        y_pad = 0.1 
        y_max = 20
        self.ax.set_ylim(-y_max - y_pad, y_max + y_pad)
        self.ax.set_xlim(max(0, t - self.x_window), t)

        # Update the indicator color based on the threshold
        if self.ydata[3, -1] > 5:
            self.indicator.set_color('red')
            self.indicator.set_alpha(0.2)
        else:
            self.indicator.set_alpha(0.0)

        return self.ln1, self.ln2, self.ln3, self.ln4

    def start(self, interval=10):
        self.ani = FuncAnimation(self.fig, self.update, frames=self.source, cache_frame_data=False, 
                                 init_func=self.init, blit=False, interval=interval)
        plt.show()



class Measurement(BaseModel):
    x: float = 0
    y: float = 0
    z: float = 0
    latency: float = 0

    class Config:
        validate_assignment = True


class MeasurementClient:

    measurement = Measurement()

    logger = logging.getLogger("measurement-server")
    update_rate = False

    server_address = ("192.168.0.116", 5555)

    _last_receive_time = None
    _delta = timedelta(0)
    _socket = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
    _socket.setblocking(False)

    num = 0

    @property
    def rate(self):
        delta_sec = self._delta.seconds + self._delta.microseconds * 10e-7
        return 1 / delta_sec if delta_sec else 0.0

    def generator(self):
        yield self.num, self.measurement.x, self.measurement.y, self.measurement.z

    def __init__(self, ip, port):
        self._socket.bind((ip, port))
        self._thread = Thread(target=self._loop)
        self._thread.start()

    def _loop(self):
        while True:
            data = self.communicate()
            if data is not None and self.update_rate:
                self._update_rate()
            time.sleep(0.001)

    def getData(self) -> Optional[str]:
        # Get lastest reading, emptying any lingering input
        data = None
        while True:
            try:
                data, _ = self._socket.recvfrom(1024)
            except BlockingIOError:
                break
            self.logger.debug("Threw away one round of data")

        return data

    def communicate(self):
        data = self.getData()
        if data is not None:
            self.parse_message(data.decode())
            self.num += 1
        return data

    def parse_message(self, message: str):
        if not message:
            print("Empty message!")
            return

        message_type = message[0]
        if message_type == "D":
            x, y, z, lat = message[1:].replace(",", ".").replace("−", "-").split(";")
            self.measurement.x = float(x)
            self.measurement.y = float(y)
            self.measurement.z = float(z)
            self.measurement.latency = float(lat)
        elif message_type == "L":
            self._socket.sendto(message[1:].encode(), self.server_address)
        else:
            print("Unsupported message!")

    def _update_rate(self):
        now = datetime.now()
        if self._last_receive_time is None:
            self._delta = timedelta(0)
        else:
            self._delta = now - self._last_receive_time
        self._last_receive_time = now
    


if __name__ == "__main__":
    server = MeasurementClient("192.168.0.159", 5555)
    plotter = Plotter(server.generator)
    plotter.start()
    while True:
        time.sleep(0.01)

    
