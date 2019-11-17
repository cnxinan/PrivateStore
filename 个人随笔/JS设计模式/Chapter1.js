//1.1 函数也是变量- 这种声明是全局变量，容易被被人写的函数覆盖，或者覆盖掉别人的函数。
//是否有变量提升?是
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

//1.6 类
var CheckObject = function () {
  this.checkName = function () {}
  this.checkEmail = function () {}
  this.checkPassword = function () {}
}