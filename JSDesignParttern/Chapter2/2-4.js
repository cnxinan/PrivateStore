//检察长模式-安全的创建类。
var Book = function (price) {
    if (this instanceof Book) {
        this.price = price;
    } else {
        return new Book(price);
    }
}

var book = Book(100);
console.log(book.price);