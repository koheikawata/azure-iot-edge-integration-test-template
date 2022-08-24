import threading
import signal
import json
import os
import asyncio
from azure.iot.device.aio import IoTHubDeviceClient, IoTHubModuleClient
from azure.iot.device import Message, MethodResponse
from typing import Any, Dict

import rclpy
from rclpy.node import Node
from std_msgs.msg import String


class Ros2PublisherClient(Node):
    def __init__(self):
        super().__init__('ros2_publisher')
        self.publisher_ = self.create_publisher(String, os.getenv('ROS_TOPIC_NAME'), 10)
    
    def ros2_publisher(self, file_generator_request: str):
        msg = String()
        msg.data = file_generator_request
        self.publisher_.publish(msg)
        self.get_logger().info('Publishing: "%s"' % msg.data)


async def request_weather_report(payload: dict, module_client: IoTHubModuleClient):
    try:
        json_string = json.dumps(payload)
        message = Message(json_string)
        await module_client.send_message_to_output(message, 'reportRequest')
        return {"Response": "Send weather report request for {}".format(payload['city'])}, 200
    except Exception as e:
        print(e)
        return {"Response": "Invalid parameter"}, 400


async def request_download(payload: dict, module_client: IoTHubModuleClient):
    try:
        json_string = json.dumps(payload)
        message = Message(json_string)
        await module_client.send_message_to_output(message, 'updateRequest')
        return {"Response": "Send download request for {}".format(payload['fileName'])}, 200
    except Exception as e:
        print(e)
        return {"Response": "Invalid parameter"}, 400


request_method_list: Dict[str, Any] = {
    'request_weather_report': request_weather_report,
    'request_download': request_download
}


def create_client(ros2_publisher_client: Ros2PublisherClient):
    module_client = IoTHubModuleClient.create_from_edge_environment()

    async def method_request_handler(method_request):
        print('Direct Method: ', method_request.name, method_request.payload)
        if method_request.name in request_method_list:
            response_payload, response_status = await request_method_list[method_request.name](method_request.payload, module_client)
        else:
            response_payload = {"Response": "Direct method {} not defined".format(method_request.name)}
            response_status = 404

        method_response = MethodResponse.create_from_method_request(method_request, response_status, response_payload)
        await module_client.send_method_response(method_response)
    
    async def receive_message_handler(message_received):
        if message_received.input_name == 'reportResponse':
            message_telemetry = Message(
                data=message_received.data.decode('utf-8'),
                content_encoding='utf-8',
                content_type='application/json',
            )
            await module_client.send_message_to_output(message_telemetry, 'telemetry')
            print("Weather report sent to IoT Hub")

            ros2_publisher_client.ros2_publisher(message_received.data.decode('utf-8'))
            print("ROS2 message sent to FileGenerator")

        elif  message_received.input_name == 'updateResponse':
            message_telemetry = Message(
                data=message_received.data.decode('utf-8'),
                content_encoding='utf-8',
                content_type='application/json',
            )
            await module_client.send_message_to_output(message_telemetry, 'telemetry')
            print("Download status sent to IoT Hub")
        else:
            print("Message received on unknown input: {}".format(message_received.input_name))

    try:
        module_client.on_method_request_received = method_request_handler
        module_client.on_message_received = receive_message_handler
    except:
        module_client.shutdown()
        raise

    return module_client


def main():
    def module_termination_handler(signal, frame):
        print ("IoTHubClient sample stopped")
        stop_event.set()

    rclpy.init()
    ros2_publisher_client = Ros2PublisherClient()

    stop_event = threading.Event()
    signal.signal(signal.SIGTERM, module_termination_handler)
    module_client = create_client(ros2_publisher_client)
    try:
        stop_event.wait()
    except Exception as e:
        print("Unexpected error %s " % e)
        raise
    finally:
        print("Shutting down client")
        module_client.shutdown()
        ros2_publisher_client.destroy_node()
        rclpy.shutdown()


if __name__ == '__main__':
    main()