from ctypes import *
from ctypes.wintypes import UINT
from ctypes.wintypes import DWORD

timeproc = WINFUNCTYPE(None, c_uint, c_uint, DWORD, DWORD, DWORD)
timeSetEvent = windll.winmm.timeSetEvent
timeKillEvent = windll.winmm.timeKillEvent


class mmtimer:
    def Tick(self):
        self.tickFunc()

        if not self.periodic:
            self.stop()

    def CallBack(self, uID, uMsg, dwUser, dw1, dw2):
        if self.running:
            self.Tick()

    def __init__(self, interval, tickFunc, stopFunc=None, resolution=0, periodic=True):
        self.interval = UINT(interval)
        self.resolution = UINT(resolution)
        self.tickFunc = tickFunc
        self.stopFunc = stopFunc
        self.periodic = periodic
        self.id = None
        self.running = False
        self.calbckfn = timeproc(self.CallBack)

    def start(self, instant=False):
        if not self.running:
            self.running = True
            if instant:
                self.Tick()

            self.id = timeSetEvent(self.interval, self.resolution,
                                   self.calbckfn, c_ulong(0),
                                   c_uint(self.periodic))

    def stop(self):
        if self.running:
            timeKillEvent(self.id)
            self.running = False

            if self.stopFunc:
                self.stopFunc()
				
				

				