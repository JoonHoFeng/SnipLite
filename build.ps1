$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
& $compiler /nologo /codepage:65001 /target:winexe /win32manifest:app.manifest /win32icon:app.ico /resource:app.ico,App.ico /out:SnipLite.exe /reference:System.Drawing.dll /reference:System.Windows.Forms.dll LightShot.cs
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
