import re

with open('C:/Users/prata/agent01/MainWindow.xaml', 'r') as f:
    content = f.read()

# Find the problematic section and fix missing </Setter>
content = content.replace('</Setter.Value>\n                                </Style>\n                            </Button.Resources>', '</Setter.Value>\n                                </Setter>\n                            </Style>\n                            </Button.Resources>')

with open('C:/Users/prata/agent01/MainWindow.xaml', 'w') as f:
    f.write(content)

print('Fixed XML')