with open('C:/Users/prata/agent01/MainWindow.xaml', 'r') as f:
    lines = f.readlines()

for i, line in enumerate(lines):
    if 'GradientStop Color="#3B82F6" Offset="0" Opacity="0.3"' in line:
        lines[i] = line.replace(' Opacity="0.3"', '')
        break

with open('C:/Users/prata/agent01/MainWindow.xaml', 'w') as f:
    f.writelines(lines)
print('Fixed opacity')