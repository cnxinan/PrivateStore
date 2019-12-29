//封装
var Book = function (id, name, price) {
    //私有属性
    var material = 'Paper';

    //公有属性
    this.id = id;
    this.name = name;
    this.price = price;

    //公有方法
    this.getMaterial = function(){
        return material;
    }
}
//类静态公有属性
Book.isChinese = true;
//类静态公有方法
Book.changeLanguage = function(){
    Book.isChinese  = false;
}

Book.prototype = {
    //公有属性
    isPublish: true,
    //公有方法
    display:function(msg){
        console.log("Displayed!%",msg);
    }
}

//Test
var book = new Book(1,'Js Parttern', 100);
book.display(book.name);
book.display(book.material);
book.display(book.getMaterial());


