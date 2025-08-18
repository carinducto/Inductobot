#!/usr/bin/env python3
"""Simple test script to verify UAS-WAND simulator connection"""

import socket
import json
import struct
import time

def send_message(sock, data):
    """Send data with length prefix"""
    message = data.encode('utf-8')
    length = struct.pack('<I', len(message))  # 4-byte little-endian length prefix
    sock.sendall(length + message)

def receive_message(sock):
    """Receive data with length prefix"""
    # Read 4-byte length prefix
    length_data = sock.recv(4)
    if len(length_data) != 4:
        return None
    
    length = struct.unpack('<I', length_data)[0]
    
    # Read the actual message
    message = b''
    while len(message) < length:
        chunk = sock.recv(length - len(message))
        if not chunk:
            break
        message += chunk
    
    return message.decode('utf-8')

def test_simulator():
    """Test connection to simulated UAS-WAND device"""
    host = '127.0.0.1'
    port = 8080
    
    try:
        print(f"Connecting to {host}:{port}...")
        sock = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
        sock.connect((host, port))
        print("Connected successfully!")
        
        # Test device info command
        test_command = {
            "endpoint": "/info",
            "method": "GET",
            "payload": None
        }
        
        print(f"Sending command: {test_command}")
        send_message(sock, json.dumps(test_command))
        
        response = receive_message(sock)
        print(f"Received response: {response}")
        
        # Parse response
        response_data = json.loads(response)
        if response_data.get('isSuccess'):
            print("✅ Device info command successful!")
            print(f"Device: {response_data['data']['name']}")
            print(f"IP: {response_data['data']['ipAddress']}")
            print(f"Firmware: {response_data['data']['firmwareVersion']}")
        else:
            print("❌ Device info command failed")
            print(f"Error: {response_data.get('message', 'Unknown error')}")
        
        # Test keep-alive command
        keepalive_command = {
            "endpoint": "/ping",
            "method": "GET", 
            "payload": None
        }
        
        print(f"\nSending keep-alive: {keepalive_command}")
        send_message(sock, json.dumps(keepalive_command))
        
        response = receive_message(sock)
        print(f"Received response: {response}")
        
        response_data = json.loads(response)
        if response_data.get('isSuccess'):
            print("✅ Keep-alive command successful!")
        else:
            print("❌ Keep-alive command failed")
            
        sock.close()
        print("\nConnection test completed successfully! ✅")
        
    except Exception as e:
        print(f"❌ Connection test failed: {e}")
        return False
    
    return True

if __name__ == "__main__":
    print("UAS-WAND Simulator Connection Test")
    print("=" * 40)
    test_simulator()