import threading
import time
from arduino import *
from bt import *
from algo import *
from imgcam2 import *
from ir import *
import logging
from threading import Lock

class RPI_MAIN(threading.Thread):

        def __init__(self):
                self.steps = 0
                self.IMG = IMG()
                self.imgCoordinates = []

                logging.basicConfig(filename = "debug.log",
                                    level = logging.DEBUG,
                                    format = "%(asctime)s.%(msecs)03d - %(funcName)s: %(message)s",
                                    datefmt = "%Y-%m-%d %H:%M:%S"
                                    )
                logging.info('Log file created.')
                
                threading.Thread.__init__(self)

                self.arduino_thread = arduino()
                self.bt_thread = bt()
                self.algo_thread = algo()
                self.IR_thread = ir()
                self.IR_lock = threading.Lock()

                arduinoInit = threading.Thread(target = self.arduino_thread.connect, name = 'arduino_thread')
                btInit = threading.Thread(target = self.bt_thread.connect, name = 'bt_thread')
                algoInit = threading.Thread(target = self.algo_thread.init_connection, name = 'algo_thread')
                IRInit = threading.Thread(target = self.IR_thread.connect, name = 'IR_thread')

                arduinoInit.daemon = True
                btInit.daemon = True
                algoInit.daemon = True
                IRInit.daemon = True

                arduinoInit.start()
                btInit.start()
                algoInit.start()
                IRInit.start()

                time.sleep(1)

                while not (self.algo_thread.is_algo_connected() and self.bt_thread.is_bt_connected() and self.arduino_thread.is_arduino_connected() and self.IR_thread.is_IR_connected()):
                        time.sleep(10)

        #######################IR################################
        def writeIR(self, img):
                self.IR_thread.write(img)
                logging.debug('Write to IR: {}'.format(img))
        
        def readIR(self):
                try:
                        while True:
                                IRmsg = self.IR_thread.read()
                                logging.debug('Read from IR: {}'.format(IRmsg))
                                if IRmsg is not '':
                                        self.imgCoordinates.append(IRmsg)
                except Exception as e:
                        if 'NoneType' in str(e):
                                time.sleep(2)
                        else:
                                logging.error('Read Error from IR: {}'.format(e))

        ################ALGO#######################

        def readAlgo(self):
                try:
                        while True:
                                AlgoMsg = self.algo_thread.read()
                                self.steps = self.steps + 1
                                logging.debug('Step: {}, Read from Algo: {}'.format(self.steps, AlgoMsg))
                                self.parseAlgoMsg(AlgoMsg)
                except Exception as e:
                        if 'NoneType' in str(e):
                                time.sleep(2)
                        else:
                                logging.error('Read Error from Algo: {}'.format(e))
                
        def writeAlgo(self, msg):
                self.algo_thread.write(msg)
                logging.debug('Write to Algo: {}'.format(msg))

        def parseAlgoMsg(self, msg):
                i = 0
                while True:
                        length = int(msg[i:3+i])
                        header = msg[3+i:6+i]
                        data = msg[6+i:length+3+i]
                        dataToSend = msg[3+i:length+3+i]
                        eof = length+3+i

                        #print('Length: {}'.format(length))
                        #print('Header: {}'.format(header))
                        #print('Data: {}'.format(data))
                        #print('Message to send: {}'.format(dataToSend))
                        #print('EOF: {}'.format(eof))

                        if 'A1:' in header or 'A3:' in header:
                                self.writeArduino(data)
                        elif 'B4:' in header:
                                self.writeBT(dataToSend) 
                        elif 'B5:' in header:
                                finalCoor = self.imgCoordinates.join()
                                finalString = '{}{}'.format(dataToSend, finalCoor)
                                self.writeBT(finalString)
                        elif 'C1:' in header:
                                self.writeArduino(data)
                        elif 'D2:' in header:
                                self.IR_lock.acquire()
                                img = self.camera.takePic(data) # data is coordinates
                                self.IR_lock.release()
                                self.writeIR(img)

                        if eof >= len(msg):
                                break
                        else:
                                i = length+3+i

        ##############BLUETOOTH####################

        def readBT(self):
                while True:
                        retry = False
                        try:
                                while True:
                                        BTmsg = self.bt_thread.read_bt()
                                        logging.debug('Read from BT: {}'.format(BTmsg))
                                        if 'B3:' in BTmsg:
                                                self.writeAlgo(BTmsg)
                                        elif 'B1:' in BTmsg:
                                                self.writeAlgo(BTmsg)
                                        elif 'B2:' in BTmsg:
                                                self.writeAlgo(BTmsg)

                        except AttributeError as a:
                                if 'client_sock'in str(a):
                                        pass
                                else:
                                        print(str(a))
                                retry = True
                                

                        except Exception as e:
                                logging.error('BT Message Error: {}'.format(e))
                                retry = True

                        if not retry:
                                break

        def writeBT(self, msg):
                self.bt_thread.send_bt(msg)
                logging.debug('Write to BT: {}'.format(msg))

        ###############ARDUINO###################

        def readArduino(self):
                try:
                        while True:
                                ARDmsg = self.arduino_thread.read_ard()
                                
                                if ARDmsg is None:
                                        continue
                                ARDmsg = ARDmsg.lstrip()
                                if (len(ARDmsg) == 0):
                                        continue
                                
                                logging.debug('Read from Arduino: {}'.format(ARDmsg))
                                if 'A2:' in ARDmsg:
                                        self.writeAlgo(ARDmsg)
                except socket.error as e:
                        print('Arduino disconnected!')
                        
        def writeArduino(self, msg):
                self.IR_lock.acquire()
                self.arduino_thread.write_ard(msg)
                self.IR_lock.release()
                logging.debug('Write to Arduino: {}'.format(msg))

        #########################################

        def initializeThreads(self):
                self.read_ARD_thread = threading.Thread(target = self.readArduino, name = 'ARD_read_thread')
                self.read_algo_thread = threading.Thread(target = self.readAlgo, name = 'Algo_read_thread')
                self.read_BT_thread = threading.Thread(target = self.readBT, name = 'BT_read_thread')
                self.read_IR_thread = threading.Thread(target = self.readIR, name = 'IR_read_thread')
               
                self.read_ARD_thread.daemon = True
                self.read_algo_thread.daemon = True
                self.read_BT_thread.daemon = True
                self.read_IR_thread.daemon = True
                logging.info('All Daemon threads initialized')
                #print('All Daemon threads initialized')
                
                self.read_ARD_thread.start()
                self.read_algo_thread.start()
                self.read_BT_thread.start()
                self.read_IR_thread.start()
                logging.info('All threads started successfully...')
                #print('All threads started successfully...')


        def keepMainAlive(self):
                while(True):
                        if not (self.read_BT_thread.is_alive()):
                                print('BT thread is not running...')
                        if not (self.read_ARD_thread.is_alive()):
                                print('ARD thread is not running...')
                        if not (self.read_algo_thread.is_alive()):
                                print('Algo thread is not running...')
                        if not (self.read_IR_thread.is_alive()):
                                print('IR thread is not running...')
                        time.sleep(1)

        def closeAllSockets(self):
                self.algo_thread.close_socket()
                self.arduino_thread.close_all_sockets()
                self.bt_thread.close_all_sockets()
                self.IR_thread.close_all_sockets()
                print('All Threads killed')


if __name__ == '__main__':
        mainThread = RPI_MAIN()
        try:
                mainThread.initializeThreads()
                mainThread.keepMainAlive()
                mainThread.closeAllSockets()

        except KeyboardInterrupt:
                mainThread.closeAllSockets()
