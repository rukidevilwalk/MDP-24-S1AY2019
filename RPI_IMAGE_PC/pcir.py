import keras
from keras import backend as K
from keras.preprocessing import image
import numpy as np
from keras.models import load_model
import tensorflow as tf

import socket
import threading
from threading import *
import time
import sys

import pickle

class Client(Thread):
    def connect(self):
        self.addr = ('192.168.24.2', 6970)
        while True:
            try:
                self.client = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
                self.client.connect(self.addr)
                print('Connected to RPI! IP : {}'.format(self.addr))
                break;

            except Exception as e:
                print('RPI Connection Error: {}'.format(e))

            print('Retrying Connection...')
            time.sleep(2)

    def read(self):
        try:
            print("Listening...")
            data = self.recvall(self.client) #28.8kB 
            coordinates_string = data[0]
            #data = self.client.recv(10000000) #28.8kB 
            datapk = pickle.loads(data[1])
            with open('data.pkl', 'wb') as f:
                pickle.dump(datapk, f)
            print(type(datapk))
            coordinates = self.get_coordinates(coordinates_string)
            print(coordinates)
            return datapk, coordinates

        except Exception as e:
            if ('client' in str(e)):
                return

            if ('Broken pipe' in str(e)):
                self.close_socket()
                print('Broken pipe from read, trying to reconnect...')
                self.init_connection()
            else:
                print('Read Error from RPI: {}'.format(e))

            time.sleep(1)

    def recvall(self, conn):
        # buf = b''
        # #header_length = conn.recv(6)
        # #length = int(header_length)
        # while len(buf) < length:
        #     data = conn.recv(1000000)
        #     if not data:
        #         print('null or empty')
        #         return data
        #     buf += data
        #     print(data)
        # return buf

        BASE_SIZE = 12
        HEADER_SIZE = 6
        full_msg = b''
        new_msg = True
        while True:
            msg = conn.recv(1000000)
            if new_msg:
                print("new msg len:",msg[BASE_SIZE:BASE_SIZE + HEADER_SIZE])
                msglen = int(msg[BASE_SIZE:BASE_SIZE + HEADER_SIZE].decode("utf-8"))
                new_msg = False

            print(f"full message length: {msglen}")

            full_msg += msg

            print(len(full_msg))

            if len(full_msg)-HEADER_SIZE-BASE_SIZE == msglen:
                print("full msg recvd")
                print(full_msg[BASE_SIZE+HEADER_SIZE:])
                return full_msg[:BASE_SIZE], full_msg[BASE_SIZE + HEADER_SIZE:]
                #new_msg = True
                #full_msg = b""

    def get_coordinates(self.msg):
        result = []
        for i in range(3):
            r = msg[i*4:i*4+4]
            result.append(r)
        return result
    

    def send(self, msg):
        try:
            msg = msg.encode('utf-8')
            self.client.sendto(msg, self.addr)
            return

        except Exception as e:
            if ('Broken pipe' in str(e)):
                self.close_socket()
                print('Broken Pipe from transmission, trying to reconnect...')
                self.connect()

    def close_socket(self):
        if self.client:
            self.client.close()
            print('Closing client socket...')
        

class Model:
    
    def __init__(self):
        self.model = self.load_trained_model() #load once 20s
                
    def load_image(self, img_path):

        img = image.load_img(img_path, target_size=(640, 480))
        img_tensor = image.img_to_array(img)                    # (height, width, channels)
        img_tensor = np.expand_dims(img_tensor, axis=0)         # (1, height, width, channels), add a dimension because the model expects this shape: (batch_size, height, width, channels)
        img_tensor /= 255.                                      # imshow expects values in the range [0, 1]
        return img_tensor
        
    def load_trained_model(self):
        #**************change path, change model name**************
        #start=time.time()
        model_name = "mdp30-997"
        model = load_model(f"{model_name}.h5",compile=False)
        #print('time taken to load model {:0.3f}'.format(time.time() - start))
        return model

    def masking(self, img_tensor, segment):
        if(segment == 0):
            ones_arr = np.ones([280,280,3], dtype = int)
            zeros_arr = np.zeros([280,360,3], dtype = int)
            mask = np.concatenate((ones_arr,zeros_arr),axis=1)
        elif(segment == 1):
            ones_arr = np.ones([280,280,3], dtype = int)
            zeros_arr = np.zeros([280,180,3], dtype = int)
            mask = np.concatenate((zeros_arr, ones_arr,zeros_arr),axis=1) 
        else:
            ones_arr = np.ones([280,280,3], dtype = int)
            zeros_arr = np.zeros([280,360,3], dtype = int)
            mask = np.concatenate((zeros_arr, ones_arr),axis=1)

        zeros_arr = np.zeros([100,640,3], dtype = int)
        mask = np.concatenate((zeros_arr,mask,zeros_arr), axis = 0)

        tf_mask = tf.convert_to_tensor(mask, np.float32)

        sess = tf.InteractiveSession()
        tf_mask = tf_mask.eval()
        masked_img = tf.math.multiply(img_tensor, tf_mask)
        masked_img = masked_img.eval()
        sess.close()

        # print(masked_img.shape)
        # masked_img /= 255
        # plt.imshow(masked_img)
        # plt.show()

        return masked_img
    
    def predict_img(self, new_image):
        #*****************change img path*************************
        ##predict##
        thisdict = {'1': 0, '10': 1, '11': 2, '12': 3, '13': 4, '14': 5, '15': 6, '2': 7, '3': 8, '4': 9, '5': 10, '6': 11, '7': 12, '8': 13, '9': 14}
        
        #new_image = self.load_image(img_path)
        pred = self.model.predict(numpy.asarray(new_image))
        print(pred)
        print(thisdict)
        maxElement = np.amax(pred)

        if maxElement>0.8:
            pred_class = np.argmax(pred, axis=-1)
            for label, p in thisdict.items():
                if p == pred_class[0]:
                    #print (label)
                    return label
        else:
            #print("nothing detected")
            return -1
            

class IR:
    def __init__(self):
        
        self.client = Client()
        self.model = Model()
        
        self.client.connect()

        self.on = True
        self.readRPIthread = threading.Thread(target = self.readRPI, name = 'read_RPI')
        self.readRPIthread.start()

    def readRPI(self):
        while self.on:
            try:
                coors, pic = self.client.read()
                for i in range(3):
                    if not coors[i] == 'null':
                        masked_img = self.model.masking(imgnum, i)
                        label_result = self.model.predict_img(masked_img)
                        if not label_result == -1:
                            print('Result! '+label_result+', at: '+coors[i])
                            self.client.send(coors[i] + label_result)

            except Exception as e:
                if 'NoneType' in str(e):
                    time.sleep(2)
                else:
                    print('Read Error from RPI: {}'.format(e))
                time.sleep(2)


    def keep_connection(self):
        while(True):
            if not (self.readRPIthread.is_alive()):
                print('Read RPI thread is not running...')
            time.sleep(2)

if __name__ == '__main__':
    ir = IR()

    try:
        ir.keep_connection()

    except KeyboardInterrupt:
        ir.client.close_socket()
        ir.on = False
        sys.exit(1)


    
    
