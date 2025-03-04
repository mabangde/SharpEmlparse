# 🚀 SharpEmlparse

[![License](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)
[![GitHub Release](https://img.shields.io/github/v/release/yourname/project)](https://github.com/yourname/project/releases)
[![Build Status](https://img.shields.io/github/actions/workflow/status/yourname/project/build.yml)](https://github.com/yourname/project/actions)



## 📌 Overview
**功能介绍**  
快速解析海量eml文件，将邮件基本信息导入sqlite数据，方便后期分析





### ✨功能介绍
#### 解析eml文件基本信息：
- eml文件hash
- eml文件大小
- 邮件标题
- 发件人
- 收件人
- 附件文件名
- 附件大小



## 🚦 Quick Start
- 基于.net Framework v4.8
- 使用前请用nuget安装所需依赖
- 打包单文件需要fody支持

## 🎯 技术介绍
- 采用分段获取文件hash 避免读取整个大文件导致缓慢
- 高速解析eml文件避免读取整个大文件
- 采用基于事务高速插入数据库
- 基于生产者消费者模式
- 300GB 400万eml文件仅耗时1小时 
