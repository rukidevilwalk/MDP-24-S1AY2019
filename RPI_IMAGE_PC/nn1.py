import keras
from keras import backend as K
from keras.preprocessing import image
import numpy as np
from keras.models import load_model
#import time

class ObjectFinder:
	
	def __init__(self):
                self.n = 1
				
	def load_image(self, img_path):

		img = image.load_img(img_path, target_size=(150, 150))
		img_tensor = image.img_to_array(img)                    # (height, width, channels)
		img_tensor = np.expand_dims(img_tensor, axis=0)         # (1, height, width, channels), add a dimension because the model expects this shape: (batch_size, height, width, channels)
		img_tensor /= 255.                                      # imshow expects values in the range [0, 1]
		return img_tensor
		
	def load_trained_model(self):
		
		#**************change path, change model name**************
		#start=time.time()
		model_name = "somemdp30-997"
		model = load_model("C:/Users/simin/Desktop/MDP/models/{}.h5".format(model_name),compile=False)
		#print('time taken to load model {:0.3f}'.format(time.time() - start))
		return model
	
	def predict_img(self,model,img_path):
		#*****************change img path*************************
		##predict##
		thisdict={'1': 0, '10': 1, '11': 2, '12': 3, '13': 4, '14': 5, '15': 6, '2': 7, '3': 8, '4': 9, '5': 10, '6': 11, '7': 12, '8': 13, '9': 14}
		new_image = self.load_image(img_path)
		pred = model.predict(new_image)
		print(pred)
		print(thisdict)
		maxElement = np.amax(pred)

		if maxElement>0.99:
			pred_class = np.argmax(pred, axis=-1)
			for label, p in thisdict.items():
				if p == pred_class[0]:
					print (label)
					return label
		else:
			print("nothing detected")
			return -1
			
	def processImg(self):
		model=self.load_trained_model() #load once 20s
		img_path = ("C:/Users/simin/Desktop/MDP/18pic.jpg")
		label = self.predict_img(model, img_path)
		print(label)
		return label
	
def main():
	of = ObjectFinder()
	of.processImg()

if __name__ == "__main__":
        main()

#take photos: down,stop,1,E,
#ok D,B,right,A,5,UP,3,left,4,2