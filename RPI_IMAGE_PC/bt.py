import socket
import time

class bt(object):

    def __init__(self):
        self.server_socket = None
        self.client_socket = None
        self.bt_connected = None


    def connect(self):
        retry = True
        while retry:

            btPort = 3

            try:
                address = 'B8:27:EB:26:99:51'
                backlog = 1
                
                self.server_sock = socket.socket(socket.AF_BLUETOOTH,
                                                 socket.SOCK_STREAM,
                                                 socket.BTPROTO_RFCOMM)
                self.server_sock.bind((address, btPort))
                #print('Bind: {}'.format(btPort))

                self.server_sock.listen(backlog)
                #print('Listening...')

                self.client_sock, client_info = self.server_sock.accept()
                print('Connected to BT')

                self.bt_connected = True
                retry = False

            except Exception as e:
                if 'Address already in use' in str(e):
                    self.server_sock.unbind()
                    self.server_sock.bind((address,btPort))
                print('BT connection error: {}'.format(e))
                retry = True

            if not retry:
                break


    def is_bt_connected(self):
        return self.bt_connected

    def read_bt(self):
        try:
            data = self.client_sock.recv(2048)
            data = data.decode('utf-8')
            return data
        except BluetoothError as e:
            print('BT Read Error: {}'.format(e))
            if ('Connected reset by peer' in str(e)):
                self.disconnect_bt()
                print('Retrying connection...')
                self.connect()

    def send_bt(self, msg):
        try:
            msg = msg.encode('utf-8')
            if self.bt_connected:
                self.client_sock.send(msg)
            else:
                print('BT not connected. Transmission failure.')
        except BluetoothError as e:
            print('BT Transmission Error: {}'.format(e))

    def disconnect_bt(self):
        try:
            if not (self.client_sock is None):
                self.client_socket.close()
                print('Closing bt client socket...')

            if not (self.server_sock is None):
                self.server_socket.close()
                print('Closing bt server socket...')
        except Exception as e:
            pass

        self.bt_connected = False
