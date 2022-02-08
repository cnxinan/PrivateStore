mysql 5.7 下载 https://dev.mysql.com/downloads/mysql/5.7.html#downloads
安装 参考 https://www.jb51.net/article/129367.htm
1 配置环境变量
2 如果目录下有data folder则删除
3 执行mysqld --initialize --user=mysql  
4 新建my.ini文件，内容网上搜
5 执行mysqld -install 注册
6 启动命令 net start mysql  停止命令 net stop mysql
7 登录 mysql -u [root] -p [password]

mysql please goto mysql folder