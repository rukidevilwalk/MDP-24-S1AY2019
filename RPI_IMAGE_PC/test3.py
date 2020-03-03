import keras
from keras import backend as K
from keras.preprocessing import image
import numpy as np
from keras.models import load_model
from matplotlib import pyplot as plt
import tensorflow as tf
#import time

class ObjectFinder:
	
	def __init__(self):
		#self.model=self.load_trained_model() #load once 20s
		pass
				
	def load_image(self):
		img_path = (f"b1.jpg")
		img = image.load_img(img_path, target_size=None)
		img_tensor = image.img_to_array(img)   
		print(img_tensor.shape) 

		return img_tensor
		# print(img_tensor)                 # (height, width, channels)
		# img_tensor = np.expand_dims(img_tensor, axis=0)         # (1, height, width, channels), add a dimension because the model expects this shape: (batch_size, height, width, channels)
		# img_tensor /= 255                                       # imshow expects values in the range [0, 1]
		# return img_tensor

	def load_trained_model(self):
		thisdict={'1': 0, '10': 1, '11': 2, '12': 3, '13': 4, '14': 5, '15': 6, '2': 7, '3': 8, '4': 9, '5': 10, '6': 11, '7': 12, '8': 13, '9': 14}
		#**************change path, change model name**************
		#start=time.time()
		model_name = "mdp30-997"
		model = load_model(f"{model_name}.h5",compile=False)
		#print('time taken to load model {:0.3f}'.format(time.time() - start))
		return model
	
	def predict_img(self,model,img_path):
		#*****************change img path*************************
		##predict##
		
		new_image = self.load_image(img_path)
		pred = model.predict(new_image)
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
			
	def processImg(self):
		
		
		label = self.predict_img(self.model, img_path)
		return label

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

if __name__ == '__main__':
    ob = ObjectFinder()
    img_tensor = ob.load_image()
    for i in range(3):
    	ob.masking(img_tensor, i)
