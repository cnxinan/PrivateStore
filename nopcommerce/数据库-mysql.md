nop 判断数据库是否安装就绪，是通过 Nop.Web.App_data 目录下是否存在文件dataSettings.json判断。

更新/添加已有实体：
    Data model: Nop.Core.Domain.Catalog.Category.cs & Nop.Data.Mapping.Catalog.CategoryMap.cs
    persentation model: Nop.Web.Areas.Admin.Models.Catalog.CategoryModel.cs
    validator: Nop.Web.Areas.Admin.Validators.Catalog.CategoryValidator.cs
    更新库：重新安装或者手动添加(已存在库)

配置存储：
​	所有配置都存到Setting表，以配置代码文件的类名+属性名作为key，并且缓存。构建配置类、读取表。

​	好处是所有不用专门建多张配置表应对多种配置，问题是每次插入/更新数据，每个属性都要打开关闭数据库连接。

