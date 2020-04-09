//子类原型对象-类式继承
function SuperClass(){
    this.superValue = true;
}

SuperClass.prototype.getSuperValue = function(){
    return this.superValue;
}

function SubClass(){
    this.subValue = false;
}

SubClass.prototype = new SuperClass();

SubClass.prototype.getSubValue = function(){
    return this.subValue;
}

console.log(SubClass.__proto__); //这里就是 Function.prototype
console.log(SubClass.__proto__.apply);

var subObj = new SubClass();

console.log(subObj.getSubValue());
console.log(subObj.getSuperValue());