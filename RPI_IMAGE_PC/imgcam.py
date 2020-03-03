
from picamera import PiCamera
from picamera.array import PiRGBArray
import cv2
import numpy as np
#from sklearn.cluster import KMeans
#import imutils
from PIL import Image
#import matplotlib.pyplot as plt
#from collections import Counter
#from skimage.color import rgb2lab, deltaE_cie76
import time

MIN_AREA = 3000

class ObjectFinder:

        def __init__(self, resolution = (1280,960)):
                self.n = 1
                self.camera=PiCamera()
                self.camera.resolution = resolution


        def get_mask(self, img , color):
                mask = np.zeros(img.shape, np.uint8)
                img = cv2.blur(img, (5, 5))
                #img = cv2.bilateralFilter(img, 11, 17, 17)
                #img = cv2.GaussianBlur(img, (5, 5), 0)
                if color=="red":
                        hsv = cv2.cvtColor(img,cv2.COLOR_BGR2HSV)
                        lower_red = np.array([0,150,100])
                        upper_red = np.array([10,255,255])
                        mask1 = cv2.inRange(hsv, lower_red, upper_red)
                        # Range for upper range
                        lower_red = np.array([170,150,100])
                        upper_red = np.array([180,255,255])
                        mask2 = cv2.inRange(hsv,lower_red,upper_red)
                        mask = mask1+mask2

                elif color=="blue":
                        hsv = cv2.cvtColor(img,cv2.COLOR_BGR2HSV)
                        lower_blue = np.array([86,100,100])
                        upper_blue = np.array([110,255,255])
                        mask = cv2.inRange(hsv, lower_blue, upper_blue)

                elif color == "green":
                        hsv = cv2.cvtColor(img,cv2.COLOR_BGR2HSV)
                        lower_green = np.array([41,100,100])
                        upper_green = np.array([85,255,255])
                        mask = cv2.inRange(hsv, lower_green, upper_green)

                elif color == "yellow":
                        hsv = cv2.cvtColor(img,cv2.COLOR_BGR2HSV)
                        lower_yellow = np.array([20,50,80])
                        upper_yellow = np.array([40,255,255])
                        mask = cv2.inRange(hsv, lower_yellow, upper_yellow)

                elif color == "white":
                        hls = cv2.cvtColor(img, cv2.COLOR_BGR2HSV)
                        sensitivity = 50
                        lower_white = np.array([0,0,255-sensitivity])
                        upper_white = np.array([255,sensitivity,255])
                        mask = cv2.inRange(hls, lower_white, upper_white)
                return mask

        def find_color_mask(self,img):
                color=["yellow","green","blue","red","white"]
                #col=[]
                #print(color)
                mask=[]
                for c in color:
                        get_mask = self.get_mask(img,c)
                        #if np.sum(get_mask)<300:continue
                        #else:
                                #col.append(c)
                        mask.append(get_mask)
                                #continue
                                #print (mask)
                return color, mask

        def detect_template_c(self,img):
                shape = np.zeros((1000,1000,1), dtype = "uint8")


                hsv = cv2.cvtColor(img,cv2.COLOR_BGR2HSV)
                lower_notblack = np.array([0, 0, 150])
                upper_notblack = np.array([255,255,255])
                mask3 = cv2.inRange(hsv, lower_notblack, upper_notblack)
                #thresh = cv2.morphologyEx(mask3, cv2.MORPH_OPEN, np.ones((3,3),np.uint8))
                _,contours,_ = cv2.findContours(mask3, cv2.RETR_EXTERNAL, cv2.CHAIN_APPROX_SIMPLE)
                #cv2.threshold(img, 127, 255, cv2.THRESH_BINARY,img)
                #cv2.drawContours(shape, contours, -1,(255,255,255),3)
                if len(contours) != 0:
                        cv2.fillPoly(shape, pts=contours, color=(255,255,255))
                #cv2.imshow("Show",shape)
                #cv2.waitKey()
                return shape

        def detect_contours(self, img):
                n=False
                contoursArea=0
                color, mask = self.find_color_mask(img)
                shape = []
                shape.append(np.zeros((1000,1000,1), dtype = "uint8"))
                shape.append(np.zeros((1000,1000,1), dtype = "uint8"))
                shape.append(np.zeros((1000,1000,1), dtype = "uint8"))
                shape.append(np.zeros((1000,1000,1), dtype = "uint8"))
                shape.append(np.zeros((1000,1000,1), dtype = "uint8"))
                rect=[None]*5
                #thresh = cv2.morphologyEx(mask, cv2.MORPH_OPEN, np.ones((3,3),np.uint8))
                for c in range(len(color)):
                        _, contours, _ = cv2.findContours(mask[c], cv2.RETR_EXTERNAL, cv2.CHAIN_APPROX_SIMPLE)
                        contours = sorted(contours, key = cv2.contourArea, reverse = True)[:1]
                        rect[c]=(self.get_bbox(contours))
                        for contour in contours:
                                if cv2.contourArea(contour)>1000:
                                        cv2.fillPoly(shape[c], pts=contours, color=(255,255,255))
                                        cv2.drawContours(shape[c], contours, -1,(0,255,0),3 )


                #cv2.threshold(shape, 127, 255, cv2.THRESH_BINARY,shape)
                return color,rect,shape

        def get_bbox(self, contours):
                #imgResized = np.zeros(img.shape[:2], np.uint8)
                #index=color_list.index(color)
                #print(color_list)
                #print(color)
                #print(index)
                '''
                for c in contours:
                        rect = cv2.boundingRect(c)
                        if rect[2] < 100 or rect[3] < 100: continue
                        #print (cv2.contourArea(c))
                        if cv2.contourArea(c)<200: continue
                        x,y,w,h = rect
                '''
                #cv2.rectangle(img,(x,y),(x+w,y+h),(0,255,0),2)
                boundingBoxes = [cv2.boundingRect(c) for c in contours]
                #print(boundingBoxes)

                return boundingBoxes

        def draw_bbox(self,img,rect):
                for i in range(len(rect)):
                        x,y,w,h = rect[i]
                        cv2.rectangle(img,(x,y),(x+w,y+h),(0,255,0),2)

        def crop(self, img, rect):
                x,y,w,h = rect
                imgCrop = img[y:(y+h), x:(x+w)]
                #imgResized = cv2.resize(imgCrop, (500,500))
                return imgCrop

        def find_similarity(self, img, template, contours, color, rect, coordinates):
                values = []
                template_list=[]
                template_id=[]
                #print(color)
                for c in range(len(color)):
                        #print(color[c])
                        if color[c]=="red":
                                values.append(cv2.matchShapes(contours[c], template[2], cv2.CONTOURS_MATCH_I2,0.0))
                                values.append(cv2.matchShapes(contours[c], template[7], cv2.CONTOURS_MATCH_I2,0.0))
                                values.append(cv2.matchShapes(contours[c], template[10],cv2.CONTOURS_MATCH_I2,0.0))
                                template_id.extend((2,8,11))
                        elif color[c]=="blue":
                                values.append(cv2.matchShapes(contours[c], template[2],cv2.CONTOURS_MATCH_I2,0.0))
                                values.append(cv2.matchShapes(contours[c], template[5],cv2.CONTOURS_MATCH_I2,0.0))
                                values.append(cv2.matchShapes(contours[c], template[13],cv2.CONTOURS_MATCH_I2,0.0))
                                template_id.extend((4,6,14))
                        elif color[c]=="green":
                                values.append(cv2.matchShapes(contours[c], template[2],cv2.CONTOURS_MATCH_I2,0.0))
                                values.append(cv2.matchShapes(contours[c], template[6],cv2.CONTOURS_MATCH_I2,0.0))
                                values.append(cv2.matchShapes(contours[c], template[11],cv2.CONTOURS_MATCH_I2,0.0))
                                template_id.extend((3,7,12))
                        elif color[c]=="yellow":
                                values.append(cv2.matchShapes(contours[c], template[4],cv2.CONTOURS_MATCH_I3,0.0))
                                values.append(cv2.matchShapes(contours[c], template[9],cv2.CONTOURS_MATCH_I3,0.0))
                                values.append(cv2.matchShapes(contours[c], template[14],cv2.CONTOURS_MATCH_I3,0.0))
                                template_id.extend((5,10,15))
                        elif color[c]=="white":
                                values.append(cv2.matchShapes(contours[c], template[2],cv2.CONTOURS_MATCH_I2,0.0))
                                values.append(cv2.matchShapes(contours[c], template[8],cv2.CONTOURS_MATCH_I2,0.0))
                                values.append(cv2.matchShapes(contours[c], template[12],cv2.CONTOURS_MATCH_I2,0.0))
                                template_id.extend((1,9,13))

                #print(values)
                #for i in template_list:

                    #plt.subplot(4, 5, i), plt.imshow(template[i-1], cmap='gray')

                    #print (ret)

                val, idx = min((val, idx) for (idx, val) in enumerate(values))

                #find mode
                #T = "Closest Match: {}, Value: {}, Coordinates: {}".format(template_id[idx], val, str(coordinates))
                T = "Closest Match: {}".format(template_id[idx])
                #T =  "closest match: ",template_id[idx],", value:", " coordinates:", coordinates
                if template_id[idx]==5 or template_id[idx]==10 or template_id[idx]==15:
                         rect=rect[0]
                elif template_id[idx]==3 or template_id[idx]==7 or template_id[idx]==12:
                         rect=rect[1]
                elif template_id[idx]==4 or template_id[idx]==6 or template_id[idx]==14:
                        rect=rect[2]
                elif template_id[idx]==2 or template_id[idx]==8 or template_id[idx]==11:
                        rect=rect[3]
                elif template_id[idx]==1 or template_id[idx]==9 or template_id[idx]==13:
                        rect=rect[4]

                self.draw_bbox(img,rect)
                #print (T)
                return T, val, template_id[idx]

        #cv2.imshow("Show",imgResized)
        #cv2.waitKey()
        def preview(self):
                self.camera.start_preview()
                time.sleep(2)

        def capture(self):
                output = PiRGBArray(self.camera)
                self.camera.capture(output,'bgr')
                img = output.array
                return img

        def temp_match(self, img):
                templates = [cv2.imread("{}.jpg".format(i),cv2.IMREAD_GRAYSCALE) for i in range(1,16)]
                templates = [cv2.resize(i, (50,50)) for i in templates]
                templates = [cv2.Canny(i, 50, 200) for i in templates]

                (tH, tW) = templates[0].shape[:2]
                #cv2.imshow("Template", template[0])
                #cv2.waitKey(0)
                #for imagePath in glob.glob("opencv_frame_0.jpg"):

                #img=capture()
                #img=cv2.imread("opencv_frame_0.jpg")
                gray = cv2.cvtColor(img, cv2.COLOR_BGR2GRAY)
                found=None
                found_list=[]
                maxVal_list=[]
                # loop over the scales of the image


                for template in templates:
                        for scale in np.linspace(0.1, 1.0, 50)[::-1]:
                                # resize the image according to the scale, and keep track
                                # of the ratio of the resizing
                                resized = imutils.resize(gray, width = int(gray.shape[1] * scale))
                                r = gray.shape[1] / float(resized.shape[1])

                                # if the resized image is smaller than the template, then break
                                # from the loop
                                if resized.shape[0] < tH or resized.shape[1] < tW:
                                        break
                                edged = cv2.Canny(resized, 50, 200)
                                result = cv2.matchTemplate(edged, template, cv2.TM_CCOEFF_NORMED)

                                #####threshold for false positives

                                _, maxVal, _, maxLoc = cv2.minMaxLoc(result)

                                # draw a bounding box around the detected region
                                #clone = np.dstack([edged, edged, edged])
                                #cv2.rectangle(clone, (maxLoc[0], maxLoc[1]),
                                #    (maxLoc[0] + tW, maxLoc[1] + tH), (0, 0, 255), 2)
                                #cv2.imshow("Visualize", clone)
                                #cv2.waitKey(0)
                                #scale
                                if found is None or maxVal > found[0]:
                                        found = (maxVal, maxLoc, r)

                        found_list.append(found)
                        maxVal_list.append(found[0])
                        #print(maxVal_list)
                        # unpack the bookkeeping variable and compute the (x, y) coordinates
                        # of the bounding box based on the resized ratio

                val, idx = max((val, idx) for (idx, val) in enumerate(reversed(maxVal_list)))
                #print(val)
                match = 15-idx
                #print(match)
                if val <0.5:
                        return img, -1

                else:

                        (_, maxLoc, r) = found_list[15-idx-1]

                        (startX, startY) = (int(maxLoc[0] * r), int(maxLoc[1] * r))
                        (endX, endY) = (int((maxLoc[0] + tW) * r), int((maxLoc[1] + tH) * r))

                        # draw a bounding box around the detected result and display the image
                        #cv2.rectangle(img, (startX, startY), (endX, endY), (0, 0, 255), 2)

                        #cv2.putText(img, str(similar), (50,50), cv2.FONT_HERSHEY_SIMPLEX, 0.4, (100,255,100), 1)
                        #cv2.imshow("Image", img)
                        #cv2.waitKey(0)
                        x=startX
                        y=startY 
                        w=endX-startX
                        h=endY-startY
                        imgCrop = img[y:(y+h), x:(x+w)]
                        return imgCrop, match

        def takePicture(self, n, coordinates):
                img_counter=3
                template = []
                template = [cv2.imread("templates/{}.jpg".format(i)) for i in range(1,16)]
                template = [self.detect_template_c(i) for i in template]
                #print(template)
                #img= cv2.imread("pics0.jpg")
                img = self.capture()
                imgsave = Image.fromarray(img, "RGB")
                #imgsave = cv2.cvtColor(imgsave, cv2.COLOR_BGR2RGB)
                imgsave.save('pics/pic{}.jpg'.format(n))
                imgCrop,match_t = self.temp_match(img)
                #self.camera.stop_preview()
                #img = cv2.imread("opencv_frame_{}.jpg".format(img_counter))
                #img = self.crop(img, (600,100,1200,800))
                color, rect, shape = self.detect_contours(imgCrop)
                #print(rect)


                #img_name = "opencv_img_{}.jpg".format(img_counter)
                #img_counter+=1
                #cv2.imwrite(img_name, img)
                #print("pics{}.jpg written!".format(n))
                output, score, match_c = self.find_similarity(imgCrop, template, shape, color, rect, coordinates)
                if match_t == -1 and score < 0.009: #template matching failed, but contour matching ok
                        cv2.putText(img, str(output), (50,50), cv2.FONT_HERSHEY_SIMPLEX, 0.4, (100,255,100), 1)
                        imageID=match_c
                        
                elif match_t== 10:
                        output = "closest match: 10"
                        cv2.putText(img, str(output), (50,50), cv2.FONT_HERSHEY_SIMPLEX, 0.4, (100,255,100), 1)
                        imageID=match_t
                        
                elif match_c==match_t: #both ok
                        cv2.putText(img, str(output), (50,50), cv2.FONT_HERSHEY_SIMPLEX, 0.4, (100,255,100), 1)
                        imageID=match_c
                        
                else:
                        output="Nothing detected"
                        cv2.putText(img, str(output), (50,50), cv2.FONT_HERSHEY_SIMPLEX, 0.4, (100,255,100), 1)
                        imageID= ""
                #rect = self.draw_bbox(img, contours,color,color_list)
                im = Image.fromarray(img, "RGB")
                #im = cv2.cvtColor(im, cv2.COLOR_BGR2RGB)
                im.save('pics/picFinal{}.jpg'.format(n))

                #cv2.imshow("yellow",shape[0])
                #cv2.imshow("green",shape[1])
                #cv2.imshow("blue",shape[2])
                #cv2.imshow("red",shape[3])
                #cv2.imshow("white",shape[4])
                #cv2.imshow("1",img)

                #cv2.waitKey(0)
                #print(score)
                #plt.imshow(img, cmap='gray')
                #plt.show()

                x, y = coordinates[0:2], coordinates[2:]
                string_android="{},{},{}".format(imageID,x,y)
                if imageID is "":
                        return
                else:
                        return string_android

'''def main():
        coordinates="0810"
        n=1
        of = ObjectFinder()
        of.preview()

        while True:
                myInput=raw_input("take a photo")
                #print("success")
                result = of.getObject(n, coordinates)

if __name__ == "__main__":
        main()'''

