
Readme for GeoNames Gazetteer extract files

============================================================================================================

This work is licensed under a Creative Commons Attribution 4.0 License,
see https://creativecommons.org/licenses/by/4.0/
The Data is provided "as is" without warranty or any representation of accuracy, timeliness or completeness.

The data format is tab-delimited text in utf8 encoding.


Files :
-------
XX.zip                   : features for country with iso code XX, see 'geoname' table for columns. 'no-country' for features not belonging to a country.
allCountries.zip         : all countries combined in one file, see 'geoname' table for columns
cities500.zip            : all cities with a population > 500 or seats of adm div down to PPLA4 (ca 185.000), see 'geoname' table for columns
cities1000.zip           : all cities with a population > 1000 or seats of adm div down to PPLA3 (ca 130.000), see 'geoname' table for columns
cities5000.zip           : all cities with a population > 5000 or PPLA (ca 50.000), see 'geoname' table for columns
cities15000.zip          : all cities with a population > 15000 or capitals (ca 25.000), see 'geoname' table for columns
alternateNamesV2.zip     : alternate names with language codes and geonameId, file with iso language codes, with new columns from and to
alternateNames.zip       : obsolete use V2, this file does not have the new columns to and from and will be removed in the future
admin1CodesASCII.txt     : names in English for admin divisions. Columns: code, name, name ascii, geonameid
admin2Codes.txt          : names for administrative subdivision 'admin2 code' (UTF8), Format : concatenated codes <tab>name <tab> asciiname <tab> geonameId
iso-languagecodes.txt    : iso 639 language codes, as used for alternate names in file alternateNames.zip
featureCodes.txt         : name and description for feature classes and feature codes 
timeZones.txt            : countryCode, timezoneId, gmt offset on 1st of January, dst offset to gmt on 1st of July (of the current year), rawOffset without DST
countryInfo.txt          : country information : iso codes, fips codes, languages, capital ,...
                           see the geonames webservices for additional country information,
                                bounding box                         : http://api.geonames.org/countryInfo?
                                country names in different languages : http:/api.geonames.org/countryInfoCSV?lang=it
modifications-<date>.txt : all records modified on the previous day, the date is in yyyy-MM-dd format. You can use this file to daily synchronize your own geonames database.
deletes-<date>.txt       : all records deleted on the previous day, format : geonameId <tab> name <tab> comment.

alternateNamesModifications-<date>.txt : all alternate names modified on the previous day,
alternateNamesDeletes-<date>.txt       : all alternate names deleted on the previous day, format : alternateNameId <tab> geonameId <tab> name <tab> comment.
userTags.zip		: user tags , format : geonameId <tab> tag.
hierarchy.zip		: parentId, childId, type. The type 'ADM' stands for the admin hierarchy modeled by the admin1-4 codes. The other entries are entered with the user interface. The relation toponym-adm hierarchy is not included in the file, it can instead be built from the admincodes of the toponym.
adminCode5.zip		: the new adm5 column is not yet exported in the other files (in order to not break import scripts). Instead it is availabe as separate file.
			  columns: geonameId,adm5code

The main 'geoname' table has the following fields :
---------------------------------------------------
geonameid         : integer id of record in geonames database
name              : name of geographical point (utf8) varchar(200)
asciiname         : name of geographical point in plain ascii characters, varchar(200)
alternatenames    : alternatenames, comma separated, ascii names automatically transliterated, convenience attribute from alternatename table, varchar(10000)
latitude          : latitude in decimal degrees (wgs84)
longitude         : longitude in decimal degrees (wgs84)
feature class     : see http://www.geonames.org/export/codes.html, char(1)
feature code      : see http://www.geonames.org/export/codes.html, varchar(10)
country code      : ISO-3166 2-letter country code, 2 characters
cc2               : alternate country codes, comma separated, ISO-3166 2-letter country code, 200 characters
admin1 code       : fipscode (subject to change to iso code), see exceptions below, see file admin1Codes.txt for display names of this code; varchar(20)
admin2 code       : code for the second administrative division, a county in the US, see file admin2Codes.txt; varchar(80) 
admin3 code       : code for third level administrative division, varchar(20)
admin4 code       : code for fourth level administrative division, varchar(20)
population        : bigint (8 byte int) 
elevation         : in meters, integer
dem               : digital elevation model, srtm3 or gtopo30, average elevation of 3''x3'' (ca 90mx90m) or 30''x30'' (ca 900mx900m) area in meters, integer. srtm processed by cgiar/ciat.
timezone          : the iana timezone id (see file timeZone.txt) varchar(40)
modification date : date of last modification in yyyy-MM-dd format


AdminCodes:
Most adm1 are FIPS codes. ISO codes are used for US, CH, BE and ME. UK and Greece are using an additional level between country and fips code. The code '00' stands for general features where no specific adm1 code is defined.
The corresponding admin feature is found with the same countrycode and adminX codes and the respective feature code ADMx.



The table 'alternate names' :
-----------------------------
alternateNameId   : the id of this alternate name, int
geonameid         : geonameId referring to id in table 'geoname', int
isolanguage       : iso 639 language code 2- or 3-characters, optionally followed by a hyphen and a countrycode for country specific variants (ex:zh-CN) or by a variant name (ex: zh-Hant); 4-characters 'post' for postal codes and 'iata','icao' and faac for airport codes, fr_1793 for French Revolution names,  abbr for abbreviation, link to a website (mostly to wikipedia), wkdt for the wikidataid, varchar(7)
alternate name    : alternate name or name variant, varchar(400)
isPreferredName   : '1', if this alternate name is an official/preferred name
isShortName       : '1', if this is a short name like 'California' for 'State of California'
isColloquial      : '1', if this alternate name is a colloquial or slang term. Example: 'Big Apple' for 'New York'.
isHistoric        : '1', if this alternate name is historic and was used in the past. Example 'Bombay' for 'Mumbai'.
from		  : from period when the name was used
to		  : to period when the name was used

Remark : the field 'alternatenames' in the table 'geoname' is a short version of the 'alternatenames' table without links and postal codes but with ascii transliterations. You probably don't need both. 
If you don't need to know the language of a name variant, the field 'alternatenames' will be sufficient. If you need to know the language
of a name variant, then you will need to load the table 'alternatenames' and you can drop the column in the geoname table.




Boundaries:
Simplified country boundaries are available in two slightly different formats:
shapes_simplified_low:
geonameId: 	The geonameId of the feature
geoJson:	The boundary in geoJson format

shapes_simplified_low.json:
similar to the abovementioned file, but fully in geojson format. The geonameId is a feature property in the geojson string.


Statistics on the number of features per country and the feature class and code distributions : http://www.geonames.org/statistics/ 


Continent codes :
AF : Africa			geonameId=6255146
AS : Asia			geonameId=6255147
EU : Europe			geonameId=6255148
NA : North America		geonameId=6255149
OC : Oceania			geonameId=6255151
SA : South America		geonameId=6255150
AN : Antarctica			geonameId=6255152


feature classes:
A: country, state, region,...
H: stream, lake, ...
L: parks,area, ...
P: city, village,...
R: road, railroad 
S: spot, building, farm
T: mountain,hill,rock,... 
U: undersea
V: forest,heath,...


If you find errors or miss important places, please do use the wiki-style edit interface on our website 
https://www.geonames.org to correct inaccuracies and to add new records. 
Thanks in the name of the geonames community for your valuable contribution.

Data Sources:
https://www.geonames.org/datasources/


More Information is also available in the geonames faq :

https://forum.geonames.org/gforum/forums/show/6.page

The forum : https://forum.geonames.org

or the google group : https://groups.google.com/group/geonames

