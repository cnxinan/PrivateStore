//这里有个问题，我们一般声明对象，都是通过{}，为何这个却是用函数做对象？难道单纯是为了面向对象？
var obj = {}; //type is object 
var fun = function() {};  //type is function
var newfun = new Function();  //type is function
console.log("obj is %s and fun is %s and newfun is %s", typeof obj, typeof fun, typeof newfun);

var Book = function(id, bookname, price){
  this.id = id;
  this.bookname = bookname;
  this.price = price;
}