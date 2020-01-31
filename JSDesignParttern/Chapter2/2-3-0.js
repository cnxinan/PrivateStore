//闭包
var Book = (function () {
    var material = "Paper";
    var bookQuentity = 0;

    if(bookQuentity > 10){
        throw new Error('We just can publish 10 books');
    }

    return function (id, name, price) {
        bookQuentity++;

        this.id = id;
        this.name = name;
        this.price = price;

        this.checkName = function () {
            if (name.indexOf("CC") > -1) {
                console.log("Right!");
            } else {
                console.log("Wrong!");
            }
        }

        this.getMaterial = function () {
            return material;
        }
    }
})();

Book.prototype = {
    isJsBook: true,
    changeBookType: function (is_js_book) {
        this.isJsBook = is_js_book;
    },
    display: function () {
        console.log(this.name);
        console.log(this.getMaterial());
        this.checkName();
    }
};

var jsBook = new Book(1, "Js Parttern", 10);
jsBook.display();
var cSharpBook = new Book(2, "C# Via CC", 100);
cSharpBook.display();