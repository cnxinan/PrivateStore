1. https://www.postgresql.org/download/linux/redhat/ 按照指引安装好
2. 切换到postgres 默认用户遇到 bash~4.2问题解决https://blog.csdn.net/testcs_dn/article/details/70482468
3. 在postgres 用户下，创建自己的测试库 createdb [dbname]

   

common commands:
1. Bash下 psql [dbname] 进入到新创建的库里面，默认进入 postgres 库
2. Internal commands
    1. List of database:  \l
    2. Quit psql: \q
    3. change db: \c [dbname];
    4. change user: \c -[username];
    5. get help: \h


sql commands:
1.  createuser [username]
2.  createdb/dropdb [dbname] 
3.  