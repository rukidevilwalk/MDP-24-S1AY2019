from picamera import PiCamera
from picamera.array import PiRGBArray

class Camera(object):

        def __init__(self, resolution = (640,480)):
                self.camera=PiCamera()
                self.camera.resolution = resolution

        def capture(self):
                output = PiRGBArray(self.camera)
                self.camera.capture(output, 'rgb')
                return output.array

        def takePic(self, coordinates):
                img = self.capture()
                print(img)
                return (img, coordinates)
