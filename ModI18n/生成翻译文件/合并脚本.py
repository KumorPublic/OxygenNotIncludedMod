import os

def remove_pot_header(filepath):
    with open(filepath, 'r', encoding='utf-8') as f:
        lines = f.readlines()

    # 检查前四行内容
    header = [
        'msgid ""\n',
        'msgstr ""\n',
        '"Application: Oxygen Not Included"\n',
        '"POT Version: 2.0"'
    ]

    # 更健壮地比较前四行内容，忽略空白和换行符差异
    if len(lines) >= 4 and all(
        lines[i].strip() == header[i].strip() for i in range(4)
    ):
        # 删除前四行
        lines = lines[4:]
        with open(filepath, 'w', encoding='utf-8') as f:
            f.writelines(lines)
        print("已删除前四行。")
    else:
        print("前四行内容不匹配，无需删除。")

def main():
    header_file = 'zh-hans.tpl'
    output_file = 'zh-hans.po'
    pot_files = [f for f in os.listdir('.') if f.endswith('.pot')]

    # 读取模板头部
    with open(header_file, 'r', encoding='utf-8') as tpl:
        header = tpl.read()

    # 收集所有.pot文件内容
    pot_contents = []
    for pot_file in pot_files:
        remove_pot_header(pot_file)
        with open(pot_file, 'r', encoding='utf-8') as pf:
            pot_contents.append(pf.read())

    # 合并写入zh-hans.po
    with open(output_file, 'w', encoding='utf-8') as out:
        out.write(header)
        out.write('\n')
        for content in pot_contents:
            out.write(content)
            out.write('\n')

if __name__ == '__main__':
    main()
    input("按回车键退出...")