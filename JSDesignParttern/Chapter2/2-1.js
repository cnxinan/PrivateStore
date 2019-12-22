//这里有个问题，我们一般声明对象，都是通过{}，为何这个却是用函数做对象？难道单纯是为了面向对象？函数也是对象
var obj = {}; //type is object 
var fun = function() {};  //type is function
var newfun = new Function();  //type is function
console.log("obj is %s and fun is %s and newfun is %s", typeof obj, typeof fun, typeof newfun);

//声明一个类Book:js里类就是对象模板，本身也是一个对象。
var Book = function(id, bookname, price){
  this.id = id;
  this.bookname = bookname;
  this.price = price;
}

//这里实质上是执行了函数,Book只是函数名，this指向了global
Book(1,'jsBook',25.5);
if(Book != undefined){
  console.log("Book name id is %s, name is %s, price is %d ", Book.id, Book.bookname, Book.price);
}else{
  console.log("global name id is %s, name is %s, price is %d ", global.id, global.bookname, global.price);
}

//给Book prototype 赋值
console.log("Book's prototype is %O",Book.prototype);
Book.prototype.display = function (){console.log("id=%s,name=%s,price=%s",this.id,this.bookname,this.price)}; //或Book.prototype = {display:function(){...}}
console.log("Book's prototype is %O",Book.prototype);

//bookTmp并不是Book的赋值，而是Book函数执行的结果
var bookTmp = Book(2,'Data Base',26.5);
if(bookTmp){
  console.log("bookTmp name id is %s, name is %s, price is %d ", bookTmp.id, bookTmp.bookname, bookTmp.price);
}else{
  console.log("global name id is %s, name is %s, price is %d ", global.id, global.bookname, global.price);
}

//book才是Book的赋值, new 关键字就是在改变this，现在的this指向了book
var book = new Book(124,'C# Book', 30);
console.log("book name id is %s, name is %s, price is %d ", book.id, book.bookname, book.price);
console.log("book's' %O", book.prototype);
console.log("book's parent prototype %O",book.__proto__);
console.log("Book's constructor %O", Book.constructor);
book.display(); //原型链