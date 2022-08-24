import os
import json
import pathlib
import datetime
import rclpy
from rclpy.node import Node

from std_msgs.msg import String


class FileGenerator(Node):

    def __init__(self):
        super().__init__('file_generator')
        self.subscription = self.create_subscription(
            String,
            os.getenv('ROS_TOPIC_NAME'),
            self.listener_callback,
            10)
        self.subscription

    def listener_callback(self, msg):
        self.get_logger().info('Received: "%s"' % msg.data)
        dict_message = json.loads(msg.data)
        city_name = dict_message['city']
        dt_now = datetime.datetime.now()
        directory_path = os.getenv('OUTPUT_DIRECTORY_PATH')
        zip_file_name = directory_path + '/' + city_name + '{month:02}{day:02}'.format(month=dt_now.month, day=dt_now.day) + '.zip'
        json_file_name = directory_path + '/' + city_name + '{month:02}{day:02}'.format(month=dt_now.month, day=dt_now.day) + '.zip.json'
        
        with open(json_file_name, "w") as outputFile:
            json.dump(dict_message, outputFile, indent=2 )
        touch_file = pathlib.Path(zip_file_name)
        touch_file.touch()


def main(args=None):
    rclpy.init(args=args)

    file_generator = FileGenerator()

    rclpy.spin(file_generator)

    file_generator.destroy_node()
    rclpy.shutdown()


if __name__ == '__main__':
    main()