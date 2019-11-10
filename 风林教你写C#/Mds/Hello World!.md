## Hello World!

世界你好!  是程序员的浪漫，它的出处是一本非常经典的C语言教程，随后几乎所有语言第一个Demo都是输出Hello World！我们也从这个例子开始，针对C# 0经验者对这段代码进行解构：

首先到任意工作目录建个文件夹，建议起名 CSharpLearing；打开Visual Studio Code，左上角菜单File->Open Folder, 然后定位到CSharpLearing目录并打开；打开Terminal，输入 `dotnet new console` ,等命令行提示完成后，输入 `dotnet run` 就能看到命令行对你的输入回应道："Hello World!"。我们不止要Hello World!,更要Create World!

现在看VSCode左侧的EXPLORER，会发现这2条命令新建了bin和obj 2个文件夹，Program.cs 和 目录名.csproj 2个新文件，下面打开Program.cs， 目前我们主要关注的就是这个文件。打开文件后

1. `using System;` 这里是导入System命名空间，以方便使用其中的功能，本例主要是Console的各个函数，命名空间的概念我们后面会讲。
2. `namespace CSharpLearing` 这是给你自己的代码声明命名空间为"CSharpLearing"，命名空间的名字可以修改。
3. `class Program` 声明一个名字为 "Program" 的类，声明和类我们后面会讲， 类的名字可以修改。
4. `static void Main(string[] args)` 这个是我们应用的入口，现在不要做修改。
5. `Console.WriteLine("Hello World!");` 我们从Terminal上看到的Hello World! 就是这句代码的功能。具体解释就是告诉控制台，输出 `Hello Wrold!` 然后换行。

现在把 `Hello World`  修改成任意你想输出的字符串, 然后在Terminal中输入 `dotnet run` 看看效果吧。

