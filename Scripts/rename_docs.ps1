cd f:\POS\docs
$mdFiles = Get-ChildItem -Filter *.md
$list = @()
foreach ($f in $mdFiles) {
    # If the file has a prefix, ignore it in tracking or clean it? Let's clean it first.
    $cleanName = $f.Name -replace '^\d{3}_', ''
    if ($f.Name -ne $cleanName) {
        Rename-Item $f.FullName -NewName $cleanName
        $f = Get-Item "f:\POS\docs\$cleanName"
    }

    # Find the earliest commit for this file anywhere in the repo
    $hist = git log --all --format='%aI' -- "**/$($f.Name)" 
    $oldest = $hist | Select-Object -Last 1

    if ([string]::IsNullOrWhiteSpace($oldest)) {
        $d = $f.CreationTime
    } else {
        $d = [datetime]::Parse($oldest)
    }

    $list += [PSCustomObject]@{ File=$f.FullName; BaseName=$f.Name; Date=$d }
}

$list = $list | Sort-Object Date
$i = 1
foreach ($item in $list) {
    $newName = "{0:D3}_{1}" -f $i, $item.BaseName
    Rename-Item $item.File -NewName $newName -Force
    $i++
}
Write-Host "DONE RENAMING"
