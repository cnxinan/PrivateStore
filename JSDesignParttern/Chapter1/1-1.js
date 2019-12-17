//这个是个基础，不需要单独运行代码，所以用一个文件来记录。

//1.1 函数也是变量- 这种声明是全局变量，容易被被人写的函数覆盖，或者覆盖掉别人的函数。
//是否有变量提升?是,因为是声明式函数
console.log(typeof checkName);
--变量提升

function checkName() {}

function checkEmail() {}

function checkPassword() {}

//1.2 另一种写法
var checkName = function () {}
var checkEmail = function () {}
var checkPassword = function () {}

//1.3 用对象收编变量
var CheckObject = {
  checkName: function () {},
  checkEmail: function () {},
  checkPassword: function () {}
}

//1.4 对象的另一种形式
var CheckObject = function () {};
CheckObject.checkName = function () {};
CheckObject.checkEmail = function () {};
CheckObject.checkPassword = function () {};

//1.5 真假对象 这个返回跟CheckObject没什么关系了，继续改造1.6
var CheckObject = function () {
  return {
    checkNmae: function () {},
    checkEmail: function () {},
    checkPassword: function () {}
  }
}

//1.6 类，问题是每个new的function 都有3个属性
var CheckObject = function () {
  this.checkName = function () {}
  this.checkEmail = function () {}
  this.checkPassword = function () {}
}

var checker = new CheckObject();

//1.7 使用prototype改造1.6, 声明方式好了，但是调用看起来不美
var CheckObject = function(){};

CheckObject.prototype.checkName = function(){};
CheckObject.prototype.checkEmail = function(){};
CheckObject.prototype.checkPassword = function(){};

/* 另一种写法
CheckObject.prototype = {
  checkNmae: function () {},
  checkEmail: function () {},
  checkPassword: function () {}
}
 */
//调用
 var checker = new CheckObject();
 checker.checkName();
 checker.checkEmail();
 checker.checkPassword();

 //1.8 利用原型链实现方法的共用，每个new的对象都利用原型链查找到
 var CheckObject = function(){};
 CheckObject.prototype = {
  checkNmae: function (name) {/* logic */ return this;},
  checkEmail: function (email) {/* logic */ return this;},
  checkPassword: function (password) {/* logic */ return this;}
}

var checker = new CheckObject();
checker.checkName().checkEmail().checkPassword();

//1.9 函数的祖先
Function.prototype.checkEmail = function(){};

var f1 = function(){}; //或 var f1 = new Function();
f1.checkEmail();
  //但是这样污染了原声Function对象，给Function加个添加方法的方法
Function.prototype.addFunction = function(name, fn){
  this[name] = fn;
}
var f2 = function(){}; //或 var f2 = new Function();
f2.addFunction('checkName', function(){});
f2.addFunction('checkEmail', function(){});
f2.addFunction('checkPassword', function(){});

f2.checkName();
f2.checkEmail();
f2.checkPassword();

//1.10 改成链式添加
Function.prototype.addFunction = function(name, fn){
  this[name] = fn;
  return this;
}
var f3 = function(){}; //或 var f3 = new Function();
f3.addFunction('checkName', function(){}).addFunction('checkEmail', function(){}).addFunction('checkPassword', function(){});

//