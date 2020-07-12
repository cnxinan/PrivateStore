--显示engines
SHOW ENGINES;
--显示数据库
SHOW DATABASES;
--切换数据库
USE [database];
--显示表所有表
SHOW TABLES;
--显示当前库
SELECT database();

CREATE TABLE `t_dept` (
	`id` INT(11) NOT NULL AUTO_INCREMENT,
	`deptName` VARCHAR(30) DEFAULT NULL,
	`address` VARCHAR(40) DEFAULT NULL,
	PRIMARY KEY (`id`)    
) ENGINE = INNODB AUTO_INCREMENT=1 DEFAULT CHARSET=utf8;

INSERT INTO `t_dept`(deptName, address) VALUES('泰山派','山东泰山');
INSERT INTO `t_dept`(deptName, address) VALUES('华山派','陕西华阴');
INSERT INTO `t_dept`(deptName, address) VALUES('少林派','河南嵩山');
INSERT INTO `t_dept`(deptName, address) VALUES('武当派','湖北武当山');
INSERT INTO `t_dept`(deptName, address) VALUES('丐帮','河南洛阳');
INSERT INTO `t_dept`(deptName, address) VALUES('燕子坞','江西鄱阳湖');
INSERT INTO `t_dept`(deptName, address) VALUES('万马堂','辽宁鸡西');


CREATE TABLE `t_emp` (
	`id` INT(11) NOT NULL AUTO_INCREMENT,
	`name` VARCHAR(20) DEFAULT NULL,
	`age` INT(3) DEFAULT NULL,
	`deptId` int(11) DEFAULT NULL,
	`empno` int NOT NULL,
	PRIMARY KEY (`id`),
	KEY `idx_dept_id` (`deptId`)
) ENGINE = INNODB AUTO_INCREMENT=1 DEFAULT CHARSET=utf8;
--泰山派
INSERT INTO `t_emp`(`name`,age,deptId,empno) VALUES('天门',67,1,10007);
INSERT INTO `t_emp`(`name`,age,deptId,empno) VALUES('天松',65,1,10008);
INSERT INTO `t_emp`(`name`,age,deptId,empno) VALUES('玉矶子',55,1,10010);
INSERT INTO `t_emp`(`name`,age,deptId,empno) VALUES('玉磬子',53,1,10011);
INSERT INTO `t_emp`(`name`,age,deptId,empno) VALUES('玉音子',52,1,10012);
--华山派
INSERT INTO `t_emp`(`name`,age,deptId,empno) VALUES('岳不群',55,2,20027);
INSERT INTO `t_emp`(`name`,age,deptId,empno) VALUES('宁中则',47,2,20028);
INSERT INTO `t_emp`(`name`,age,deptId,empno) VALUES('令狐冲',28,2,20030);
INSERT INTO `t_emp`(`name`,age,deptId,empno) VALUES('岳灵珊',23,2,20031);
INSERT INTO `t_emp`(`name`,age,deptId,empno) VALUES('林平之',22,2,20043);
--少林派
INSERT INTO `t_emp`(`name`,age,deptId,empno) VALUES('方证',65,3,30020);
INSERT INTO `t_emp`(`name`,age,deptId,empno) VALUES('方生',63,3,20037);
INSERT INTO `t_emp`(`name`,age,deptId,empno) VALUES('方快',54,3,20045);
--武当派
INSERT INTO `t_emp`(`name`,age,deptId,empno) VALUES('肾虚',67,4,40127);
INSERT INTO `t_emp`(`name`,age,deptId,empno) VALUES('冲虚',68,4,40227);
INSERT INTO `t_emp`(`name`,age,deptId,empno) VALUES('空虚',67,4,40327);
--丐帮
INSERT INTO `t_emp`(`name`,age,deptId,empno) VALUES('洪七公',67,5,50327);
INSERT INTO `t_emp`(`name`,age,deptId,empno) VALUES('乔峰',23,5,50327);
INSERT INTO `t_emp`(`name`,age,deptId,empno) VALUES('全冠清',21,5,50327);
--燕子坞
INSERT INTO `t_emp`(`name`,age,deptId,empno) VALUES('令狐飞雪',41,6,60327);
INSERT INTO `t_emp`(`name`,age,deptId,empno) VALUES('令狐匏',41,6,60127);
INSERT INTO `t_emp`(`name`,age,deptId,empno) VALUES('令狐燕',41,6,60527);
--无门派
INSERT INTO `t_emp`(`name`,age,deptId,empno) VALUES('韦小宝',21,NULL,00523);
INSERT INTO `t_emp`(`name`,age,deptId,empno) VALUES('陈近南',51,NULL,00526);
INSERT INTO `t_emp`(`name`,age,deptId,empno) VALUES('阿珂',18,NULL,00328);

--7种join
	--1 所有有门派人员信息
	select * from t_emp a join t_dept b on a.deptId=b.id;
	--2 列出所有用户，并显示机构信息
	select * from t_emp a left join t_dept b on a.deptId=b.id;
	--3 列出所有门派
	select * from t_dept;
	--4 列出所有无门派人员
	select * from t_emp where deptId is null;
	select a.* from t_emp a left join t_dept b on a.deptId=b.id where b.id is null;
	--5 列出所有无人门派
	select * from t_dept a left join t_emp b on a.id=b.deptId where b.id is null;
	--6 列出所有人员和门派对照管理(全连接) --union 去重 union all 不去重
	select a.*,b.* from t_emp a left join t_dept b on a.deptId=b.id
	union
	select b.*,a.* from t_dept a left join t_emp b on a.id=b.deptId where b.id is null;
	--7 列出所有无门派人员 和 无人门派
	select a.*,b.* from t_emp a left join t_dept b on a.deptId=b.id where b.id is null
	union all
	select b.*,a.* from t_dept a left join t_emp b on a.id=b.deptId where b.id is null;

--新增掌门人信息
ALTER TABLE t_dept ADD dept_ceo int(11);

update t_dept set dept_ceo=(select id from t_emp where `name`='天门') where deptName='泰山派';
update t_dept set dept_ceo=(select id from t_emp where `name`='岳不群') where deptName='华山派';
update t_dept set dept_ceo=(select id from t_emp where `name`='方证') where deptName='少林派';
update t_dept set dept_ceo=(select id from t_emp where `name`='冲虚') where deptName='武当派';
update t_dept set dept_ceo=(select id from t_emp where `name`='洪七公') where deptName='丐帮';
update t_dept set dept_ceo=(select id from t_emp where `name`='令狐飞雪') where deptName='燕子坞';

	--求各派掌门人信息
	select a.deptName, b.name from t_dept a inner join t_emp b on a.dept_ceo=b.id;
	--求掌门平均年龄
	select AVG(b.age) as average_age from t_dept a inner join t_emp b on a.dept_ceo=b.id;
	--所有人物对应掌门信息
	--01
	select a.name,(select name from t_emp where id=b.dept_ceo) as ceo_name from t_emp a left join t_dept b on a.deptId = b.id;
	--02
	select c.name,ab.name ceo_name from t_emp c left join 
		(select a.id, b.name from t_dept a inner join t_emp b on a.dept_ceo=b.id)ab on c.deptId=ab.id;
	--03
	select ab.name,c.name ceo_name from (select a.name,b.dept_ceo from t_emp a left join t_dept b on a.deptId=b.id)ab 
		left join t_emp c on c.id=ab.dept_ceo;
	--04
	select a.name,c.name from t_emp a 
		left join t_dept b on a.deptId=b.id
		left join t_emp c on b.dept_ceo=c.id;