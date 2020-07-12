## .editorconfig文件是什么?

## 一、.editorconfig文件是什么？

在项目里，大多时候都能看到.editorconfig文件，刚开始总是忽视掉它，认为它不太重要。但是，它的存在，必定有它的理由，于是，抽空来研究一下，它是什么，能做什么。

 [官网](https://editorconfig.org/)是这么介绍EditorConfig的，“EditorConfig帮助开发人员在不同的编辑器和IDE之间定义和维护一致的编码样式。EditorConfig项目由用于定义编码样式**的文件格式**和一组**文本编辑器插件组成**，这些**插件**使编辑器能够读取文件格式并遵循定义的样式。EditorConfig文件易于阅读，并且与版本控制系统配合使用。”

不同的开发人员，不同的编辑器，有不同的编码风格，而EditorConfig就是用来协同团队开发人员之间的代码的风格及样式规范化的一个工具，而.editorconfig正是它的默认配置文件。

## 二、示例文件

## 三、注意事项、配置说明

1、如果是windows用户，如果无法创建.editorconfig文件，则需要先创建.editorconfig.文件，系统会自动重命名成.editorconfig文件。

2、EditorConfig的匹配规则是从上往下，即先定义的规则优先级比后定义的优先级要高。

3、插件安装，在GitHub上已经有了各种流行编辑器的[插件源代码](https://github.com/editorconfig/)，可根据说明安装。

4、对于VS Core，对应的插件名是[EditorConfig for VS Code](https://marketplace.visualstudio.com/items?itemName=EditorConfig.EditorConfig)。

5、这里列举了[VS Code支持的规则](https://github.com/editorconfig/editorconfig-vscode)：

- `indent_style`
- `indent_size`
- `tab_width`
- `end_of_line`
- `insert_final_newline`
- `trim_trailing_whitespace`

## 四、写在最后

总体上来说，EditorConfig的配置还比较简单，官网的文档也不难理解，使用起来，真心不错，值得推荐。



