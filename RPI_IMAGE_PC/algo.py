import socket
import sys
import time


class algo(object):

        def __init__(self):
                self.algo_connected = False
                self.port = 6969
                self.ip = '192.168.24.2'

        def close_socket(self):
                if self.client:
                        self.client.close()
                        print('Closing client socket...')
                if self.conn:
                        self.conn.close()
                        print('Closing server socket...')

                self.algo_connected = False

                print('All sockets closed.')

        def is_algo_connected(self):
                return self.algo_connected

        def init_connection(self):
                retry = True
                while retry:

                        try:
                                self.conn = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
                                self.conn.bind((self.ip, self.port))
                                self.conn.listen(1)
                                print('Listening for connection on {} : {}'.format(self.ip, self.port))
                                (self.client, self.addr) = self.conn.accept()
                                print('Connected to Algo! IP : {}'.format(self.addr))
                                self.algo_connected = True
                                retry = False

                        except Exception as e:
                                print('Algo Connection Error: {}'.format(e))
                                retry = True

                        if (not retry):
                                break

                        print('Retrying Algo Connection...')

        def read(self):
                try:
                        data = self.client.recv(2048)
                        data = data.decode('utf-8')
                        return data

                except Exception as e:
                        if ('client' in str(e)):
                                return

                        if ('Broken pipe' in str(e)):
                                self.close_socket()
                                print('Broken pipe from read, trying to reconnect...')
                                self.init_connection()
                        else:
                                print('Read Error from Algo: {}'.format(e))

                        time.sleep(1)

        def write(self, msg):
                try:
                        msg = msg.encode('utf-8')
                        self.client.sendto(msg, self.addr)
                        return

                except Exception as e:

                        if ('Broken pipe' in str(e)):
                                self.close_socket()
                                print('Broken Pipe from transmission, trying to reconnect...')
                                self.init_connection()

