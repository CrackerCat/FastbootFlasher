# 斑•Fastboot刷写者
这是一个用于刷写安卓手机固件的工具  
目前支持的固件：  
华为：UPDATE.APP OTA 更新文件  
安卓：标准OTA固件payload.bin  
小米：payload.bin OTA 更新文件 线刷bat脚本  
红米：payload.bin OTA 更新文件 线刷bat脚本  
OPPO：payload.bin OTA 更新文件  
一加：payload.bin OTA 更新文件  
真我：payload.bin OTA 更新文件  
  
目前支持的功能：  
华为：解析与提取.APP文件中的分区镜像，合并双super分区，选择分区刷写，fastboot模式跳转升级模式  
安卓：解析与提取payload.bin固件中的分区镜像，选择分区刷写  
小米：解析小米线刷固件的flash_all.bat脚本，提取payload.bin固件中的分区镜像，选择分区刷写  
红米：解析小米线刷固件的flash_all.bat脚本，提取payload.bin固件中的分区镜像，选择分区刷写  
OPPO：解析与提取payload.bin固件中的分区镜像，选择分区刷写  
一加：解析与提取payload.bin固件中的分区镜像，选择分区刷写  
真我：解析与提取payload.bin固件中的分区镜像，选择分区刷写  
  
经作者手动记时测试，此程序提取payload.bin中所有分区镜像的速度比payload-dumper-go略胜一筹，大概快15秒左右  
  
# 🛑免责声明 / Disclaimer
本软件仅供学习与研究用途，禁止用于任何非法用途。
使用本软件刷写固件、解锁设备等操作，可能会导致设备不可用、数据丢失、保修失效等风险。请用户自行承担使用本软件所带来的一切后果。
因使用本软件造成的任何设备损坏、数据丢失、法律责任或其他损失，作者概不负责。
作者明确声明：不支持也不鼓励使用本项目从事任何违法犯罪活动。

This software is provided for educational and research purposes only.
Using this software to flash firmware, unlock devices, or perform other operations may result in device malfunction, data loss, or warranty void.
You are fully responsible for any consequences arising from the use of this software.
The author shall not be held liable for any damage, legal issues, or losses caused by using this software.
The author does not support or encourage any use of this project for illegal or criminal purposes.

