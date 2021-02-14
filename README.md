# MPF-1-Tape
A tiny program written in C# (.NET Core 3.1) to convert a binary file into Multitech MPF-I tape audio.


## Syncax

The compiled program can be called with three or four parameters in order:

`mpf1tape INPUTFILENAME BASEADDRESS FILENAME [OUTPUTFILENAME]`

INPUTFILENAME - The source for the conversion - usually a .bin or .rom file.

BASEADDRESS - The base address for the load operation. The MPF-I will re-load 
the data to the original location, so this has to refer to some RAM location on your 
system. The default system RAM chip is 2048 bytes starting at 0x1800. The address is 
passed in as 1-4 hex digits without any pre- or suffix.

FILENAME - the "file name" to assign. For the load operation, the MPF-I wants a
file name to read. It will ignore any file that has a different number, allowing
for multiple files on the same tape. This is also specified as 1-4 hex digits
without pre- or suffix.

OUTPUTFILENAME - optional; if specified, indicates the .wav file name to write. If
omitted, the input filename is taken and the extension is replaced with ".wav".

*Output files are overwrittten!*

## Format

The produced WAV will have 8bit, 8kHz mono. This makes for the most easy and smallest files,
given tihe properties of the sound in question. Careufl when playing though: It is 
at "max volume" so the playback into the MPF-I has a better chance of getting the right signal.
