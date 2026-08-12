file_path_ast = r'c:\tools\mz1500_sound_driver\Mz1500SoundPlayer\Sound\MmlAst.cs'
with open(file_path_ast, 'r', encoding='utf-8') as f:
    content_ast = f.read()

content_ast = content_ast.replace('public int[] Parameters { get; set; } = new int[38]; // ALG, FB + 36 ops params', 'public int[] Parameters { get; set; } = new int[46]; // ALG, FB + 44 ops params')
with open(file_path_ast, 'w', encoding='utf-8') as f:
    f.write(content_ast)


file_path_parser = r'c:\tools\mz1500_sound_driver\Mz1500SoundPlayer\Sound\MultiTrackMmlParser.cs'
with open(file_path_parser, 'r', encoding='utf-8') as f:
    content_parser = f.read()

content_parser = content_parser.replace('int count = Math.Min(38, matches.Count);', 'int count = Math.Min(46, matches.Count);')
with open(file_path_parser, 'w', encoding='utf-8') as f:
    f.write(content_parser)
