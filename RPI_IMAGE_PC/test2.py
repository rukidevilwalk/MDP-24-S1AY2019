import keras
from keras import backend as K
from keras.preprocessing import image
import numpy as np
from keras.models import load_model
#import time

def load_image(img_path, show=False):

    img = image.load_img(img_path, target_size=(150, 150))
    img_tensor = image.img_to_array(img)                    # (height, width, channels)
    img_tensor = np.expand_dims(img_tensor, axis=0)         # (1, height, width, channels), add a dimension because the model expects this shape: (batch_size, height, width, channels)
    img_tensor /= 255.                                      # imshow expects values in the range [0, 1]

    if show:
        plt.imshow(img_tensor[0])                           
        plt.axis('off')
        plt.show()

    return img_tensor
  
thisdict={'1': 0, '10': 1, '11': 2, '12': 3, '13': 4, '14': 5, '15': 6, '2': 7, '3': 8, '4': 9, '5': 10, '6': 11, '7': 12, '8': 13, '9': 14}
#**************change path, change model name**************
#start=time.time()
model_name = "mdp30-997"
model = load_model(f"{model_name}.h5",compile=False)
#print('time take to load model {:0.3f}'.format(time.time() - start))
#*****************change img path*************************
##predict##
img_path = (f"b1.jpg")
new_image = load_image(img_path)
pred = model.predict(new_image)
print(pred)
print(thisdict)
pred_class = np.argmax(pred, axis=-1)
if pred_class>0.8:
  for label, p in thisdict.items():
      if p == pred_class[0]:
          print (label)
else:
  print("nothing detected")