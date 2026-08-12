file_path_axaml = r'c:\tools\mz1500_sound_driver\Mz1500SoundPlayer\MainWindow.axaml'
with open(file_path_axaml, 'r', encoding='utf-8') as f:
    axaml = f.read()

find_axaml = '''        </Grid>
    </Grid>
</Window>'''
repl_axaml = '''        </Grid>
        
        <!-- Log Output -->
        <TextBox Name=""LogTextBox"" Grid.Row=""1"" Grid.ColumnSpan=""2"" 
                 IsReadOnly=""True"" TextWrapping=""Wrap"" 
                 FontFamily=""Consolas, Courier New, monospace"" FontSize=""12"" 
                 Background=""#1E1E1E"" Foreground=""#D4D4D4"" 
                 Height=""120"" Margin=""0,5,0,0"" />
    </Grid>
</Window>'''

if 'Name="LogTextBox"' not in axaml:
    # Also we need to adjust Grid.RowDefinitions
    grid_def_find = '''        <Grid.RowDefinitions>
            <RowDefinition Height=""*"" />
        </Grid.RowDefinitions>'''
    grid_def_repl = '''        <Grid.RowDefinitions>
            <RowDefinition Height=""*"" />
            <RowDefinition Height=""Auto"" />
        </Grid.RowDefinitions>'''
    
    axaml = axaml.replace(grid_def_find, grid_def_repl).replace(find_axaml, repl_axaml)
    with open(file_path_axaml, 'w', encoding='utf-8') as f:
        f.write(axaml)

file_path_cs = r'c:\tools\mz1500_sound_driver\Mz1500SoundPlayer\MainWindow.axaml.cs'
with open(file_path_cs, 'r', encoding='utf-8') as f:
    cs = f.read()

play_find = '''            _player.IsMetronomeActive = MetronomeToggle.IsChecked ?? false;
            await _player.PlayAsync(MmlTextBox.Text, activeChannels);
        }'''
play_repl = '''            _player.IsMetronomeActive = MetronomeToggle.IsChecked ?? false;
            string log = await _player.PlayAsync(MmlTextBox.Text, activeChannels);
            
            var logTextBox = this.FindControl<Avalonia.Controls.TextBox>(""LogTextBox"");
            if (logTextBox != null)
            {
                logTextBox.Text = log;
            }
        }'''

if 'this.FindControl<Avalonia.Controls.TextBox>("LogTextBox")' not in cs:
    cs = cs.replace(play_find, play_repl)
    with open(file_path_cs, 'w', encoding='utf-8') as f:
        f.write(cs)

print(""Done"")