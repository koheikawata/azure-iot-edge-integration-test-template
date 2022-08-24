FROM ros:foxy-ros-base-focal

SHELL ["/bin/bash", "-c"]

WORKDIR /app

RUN apt-get update && apt-get install -y \
    python3-pip \
    python3-colcon-common-extensions

COPY . .

RUN pip install -r requirements.txt

RUN echo 'source /opt/ros/foxy/setup.bash' >> /root/.bashrc

CMD [ "python3", "-u", "./main.py" ]