"""Create static, renamed OFL font instances for .NET Framework GDI/GDI+."""
from pathlib import Path
from fontTools.ttLib import TTFont
from fontTools.varLib.instancer import instantiateVariableFont
root=Path(__file__).resolve().parent.parent
for weight,style in [(500,"Regular"),(900,"Bold")]:
    font=TTFont(root/"vendor/NotoSansSC-variable.ttf")
    instantiateVariableFont(font,{"wght":weight},inplace=True)
    # Register both faces as one family under Windows, with an explicit style link.
    names=font["name"]
    for record in list(names.names):
        if record.nameID in (1,2,3,4,6,16,17):names.names.remove(record)
    for id,value in {1:"Miyou UI",2:style,3:"MiyouUI-"+style+"-1.0",4:"Miyou UI "+style,6:"MiyouUI-"+style,16:"Miyou UI",17:style}.items():
        names.setName(value,id,3,1,0x409)
    font["OS/2"].fsSelection=(1<<5) if style=="Bold" else (1<<6)
    font["head"].macStyle=1 if style=="Bold" else 0
    font["OS/2"].usWeightClass=700 if style=="Bold" else 400
    if "STAT" in font: del font["STAT"]
    font.save(root/("assets/MiyouUI-"+style+".ttf"))
    print("Prepared",style,flush=True)
