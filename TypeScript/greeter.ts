class Student{
  fullname:string;
  constructor(
    public name:string,
    public middleInitial: string,
    public age:number
  ){
    this.fullname = "Hello," + name + " " + middleInitial + " "+age;
  }
}

interface Person{
  name:string;
  age:number;
}

function greeter(person:Person){
  return "Hello," + person.name + " " +person.age;
}

let user = new Student ("FL4K","keety",123);

document.body.textContent = greeter(user);