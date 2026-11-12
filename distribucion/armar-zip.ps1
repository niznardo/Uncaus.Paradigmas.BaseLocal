# Arma distribucion\BaseLocal.zip con la carpeta BaseLocal (sin bin/obj) y el README.
$raiz  = Split-Path $PSScriptRoot -Parent
$temp  = Join-Path ([IO.Path]::GetTempPath()) ("BaseLocal_zip_" + [Guid]::NewGuid().ToString("N"))
$salida = Join-Path $PSScriptRoot "BaseLocal.zip"

try {
    New-Item -ItemType Directory -Path $temp | Out-Null
    robocopy (Join-Path $raiz "src\BaseLocal") (Join-Path $temp "BaseLocal") /E /XD bin obj /NFL /NDL /NJH /NJS | Out-Null
    Copy-Item (Join-Path $raiz "README.md") $temp
    if (Test-Path $salida) { Remove-Item $salida }
    Compress-Archive -Path (Join-Path $temp "*") -DestinationPath $salida
    Write-Host "Creado: $salida"
}
finally {
    if (Test-Path $temp) { Remove-Item $temp -Recurse -Force }
}
