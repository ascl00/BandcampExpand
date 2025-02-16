# BandcampExpand

This is a really simple tool. I buy a bunch of music from bandcamp.com, which gets downloaded in a zip file. I generally download both the FLAC and the AAC versions, one for Roon, and one for iTunes (ie portable). I store the music on a NAS and have mapped my windows "Music" folder to the NAS drive. Underneath "Music" I have both a FLAC and AAC directory, and want the files to end up in FLAC/bandname/albumname/.aac (or AAC/bandname/albumname/.aac|*.m4a). I got sick of doing this manually, so wrote this tool.

Doubt it is any use for anyone else, but I find it handy! It looks in a specified directory (Downloads/bandcamp/) for *.zip, inspects the zip file to see if it contains *.flac or *.m4a or *.aac, extracts the files, and copies them into the appropriate directory under "Music".
