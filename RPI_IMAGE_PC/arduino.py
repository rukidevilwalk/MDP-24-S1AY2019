from serial import *
import time

class arduino(object):

        def __init__(self):
                self.port = '/dev/ttyACM0'
                self.baud_rate = 115200
                self.arduino_connected = False

        def connect(self):
                print('Waiting for Arduino Connection...')
                retry = True
                while retry:

                        try:
                                self.ser = Serial(self.port, self.baud_rate, timeout=1)
                                print('Connected to Arduino')
                                self.arduino_connected = True
                                retry = False
                        except Exception as e:
                                print('Connection to Arduino Error: {}'.format(e))
                                retry = True
                        if (not retry):
                                break
                        print('Retrying for Arduino Connection...')
                        time.sleep(1)


        def is_arduino_connected(self):
                return self.arduino_connected

        def read_ard(self):
                try:
                        self.ser.flush()
                        data = self.ser.readline()
                        data = data.decode('utf-8')
                        return data

                except Exception as e:
                        print('Transmission from Arduino Error: {}'.format(e))


        def write_ard(self, msg):
                try:
                        if (not self.arduino_connected):
                                print('Arduino not connected')
                        else:
                                self.ser.flush()
                                msg = msg.encode('utf-8')
                                self.ser.write(msg)
                except Exception as e:
                        print('Transmittion to Arduino Error: {}'.format(e))
