dotnet new sln			--添加解决方案

dotnet sln add folder/projectname.csproj  --把项目添加到解决方案

dotnet build 				--可以build整个解决方案的所有项目

dotnet add folder1/project.csproj reference folder2/project.csproj 把2项目添加到1引用

dotnet run -p folder/project.csproj 运行特定项目

​		--no-build 可以不build直接run，速度会更快些。