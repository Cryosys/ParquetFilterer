# Summary parquet filterer
 Filters rows out of parquet files and write the altered file to disk. Allowing to remove null entries or remove them by threshold.

 # History
 The program or solution was originaly meant for filtering data out of the LAION-5B dataset as to not download everything and i could not find a simply easy to verify solution for this so i made my own app with GUI.
 While it was made with this usecase in mind, it can be used for any parquet file.

 # Limitations
 The program, as is, is quite resource intensive. A simple way to illustrate that is by looking at the parquet file you want to alter.
 You will need more than 3x its size as RAM, RAM is not necessarly the limitation here as windows will create page files if necessary (and allowed and setup by your system).
 Also you need to check if you have enough disk space. Currently the program will create a new file named the same as the source file with the appendix _filtered on it. E.g. ThisIsATestFile.parquet -> ThisIsATestFile_filtered.parquet
 
 ### Examples:
 Source file: 1GB
 Regular filters e.g. punsafe < 0.7 in case of a imagedataset url source file

 1 GB (Source size) + 1 GB (deep copy in ram) + 1GB (altered copy of the files data, should always be smaller after filtering) + Some MB for the app itself = ~3.5+ GB

 The resulting file (if not filtered at all), can be larger than the source file as the written altered file is not compressed like the source file may be.

 # Usage & Hints for filtering
 Open the ParquetFilterer.exe and enter the path to either the .parquet file or the folder containing parquet file(s). All parquet files will be edited if you enter a folder path, this can take a while, but it may be desirable if you want to edit them all the same way.

 After pressing load, the left hand side will have a list of rulesets for each column in the FIRST parquet file found. In this case you have to know that all files found are the same in the sense that they have the same columns, otherwise undesired data may be kept in the altered file.

 In the rulesets you can select what you want to filter, e.g. for punsafe lower or equal to 0.7 is usually the desired value for filtering out NSFW content, this value may be different for different databases. Here it is advised to disallow null values as the files could contain anything after downloading them later on.
 Or maybe you only want to allow images of certain sizes by settings width to equal 256. (This does not check if the image is square and, if necessary, should be checked by also settings the height to equal the same and vice versa)

 The program will start the filtering process after pressing "Start filtering...", this can take a while depending on the amount of files and they sizes. Currently the program will freeze for as long as it works, do not interrupt or close it, just wait for it to be responsive again.
 After it finished you will find the new altered file next to the source file with the _filtered appendix, currently there is no overwrite feature.