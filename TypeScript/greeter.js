var Student = /** @class */ (function () {
    function Student(name, middleInitial, age) {
        this.name = name;
        this.middleInitial = middleInitial;
        this.age = age;
        this.fullname = "Hello," + name + " " + middleInitial + " " + age;
    }
    return Student;
}());
function greeter(person) {
    return "Hello," + person.name + " " + person.age;
}
var user = new Student("FL4K", "keety", 123);
document.body.textContent = greeter(user);
