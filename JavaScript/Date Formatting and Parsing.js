// Date formatting and parsing. Note that the main formatting code is based on Datejs: http://www.datejs.com/

/**** Parse ****/

function parseDate(string, output)
{
	if(string)
	{
		var pivot = parseInt(right((new Date().getFullYear() + 5).toString(), 2), 10);
		var century = 2000;

		var formats = (
		[
			{ pattern: '^([0-9]{4})-(0?[1-9]|1[012])-(0?[1-9]|[12][0-9]|3[01])T', year: 1, month: 2, day: 3 },				// yyyy-mm-ddT - leading zeros are optional, must be four digits for year

			{ pattern: '^(0?[1-9]|1[012])[- /.](0?[1-9]|[12][0-9]|3[01])[- /.]([0-9]{1,4})', year: 3, month: 1, day: 2 },	// mm/dd/yyyy  - leading zeros are optional, any number of digits for year

			{ pattern: '^([0-9]{4})[- /.](0?[1-9]|1[012])[- /.](3[01]|[12][0-9]|0?[1-9])', year: 1, month: 2, day: 3 },		// yyyy/mm/dd  - leading zeros are optional, must be four digits for year

			{ pattern: '^([0-9]{4})(0[1-9]|1[012])(3[01]|[12][0-9]|0[1-9])', year: 1, month: 2, day: 3 }					// yyyymmdd    - leading zeros are required, must be four digits for year
		]);

		var format, regexp, result, index = 0;

		do
		{
			format = formats[index++];

			regexp = new RegExp(format.pattern, 'i');

			result = regexp.exec(string);
		}
		while(!result && (index < formats.length));

		if(result)
		{
			output.year = parseInt(result[format.year], 10);

			output.month = parseInt(result[format.month], 10);

			output.day = parseInt(result[format.day], 10);

			output.remainder = string.substr(result[0].length);

			if((output.year <= 99) && (result[format.year]) && (result[format.year].length !== 4))
			{
				if(output.year < pivot)
				{
					output.year = output.year + century;
				}
				else
				{
					output.year = output.year + century - 100;
				}
			}

			return true;
		}
	}

	output.year = 0;

	output.month = output.day = 1;

	return false;
}

function parseTime(string, output)
{
	var regexp, results;

	if(string)
	{
		regexp = new RegExp('^(0?[0-9]|1[0-9]|2[0-3]):([0-5][0-9]):?([0-5][0-9])?[:.]?([0-9]{3})? ?(AM|PM|A.M.|P.M.|A|P)?', 'i');	// hh:mm:ss:fff tt - leading zero on the hour is optional

		results = regexp.exec(string);

		if(results)
		{
			output.hour = parseInt(results[1], 10);

			output.minute = parseInt(results[2], 10);

			output.second = parseInt(results[3] ? results[3] : 0, 10);

			output.millisecond = parseInt(results[4] ? results[4] : 0, 10);

			if((output.hour <= 12) && results[5])
			{
				if(results[5].substring(0, 1).toLowerCase() === 'p')
				{
					if(output.hour !== 12)
					{
						output.hour += 12;
					}
				}
				else
				{
					if(output.hour === 12)
					{
						output.hour = 0;
					}
				}
			}

			return true;
		}
	}

	output.hour = output.minute = output.second = output.millisecond = 0;

	return false;
}

function parseDateTime(string, output)
{
	string = trim(string);

	if(parseDate(string, output))
	{
		parseTime(trim(output.remainder), output);

		return true;
	}
	else
	{
		return parseTime(string, output);
	}
}

function toDate(value)
{
	var result = null;

	if(value)
	{
		if(value.constructor === Date)
		{
			result = value;
		}
		else if(typeof value === 'string')
		{
			var output = {};

			if(parseDateTime(value, output))
			{
				result = new Date();	// We cannot pass the parameters to the constructor since it converts years less than 100 to 1900+

				result.setFullYear(output.year, output.month - 1, output.day);

				result.setHours(output.hour, output.minute, output.second, output.millisecond);
			}
		}
		else if(typeof value === 'number')
		{
			result = new Date(value);
		}
	}

	return isNaN(result) ? null : result;
}

/**** Format ****/

function formatDate(value, format)
{
	if(value)
	{
		var date = toDate(value);

		if(date)
		{
			return date.toString(format);
		}
	}

	return ''; 	
}

function longDateTime(value)
{
	return formatDate(value, 'MM/dd/yyyy HH:mm:ss.fff');
}

function mediumDateTime(value)
{
	return formatDate(value, 'MM/dd/yyyy HH:mm:ss');
}

function militaryTime(content)
{
	return formatDate(content, 'HH:mm');
}

Date.prototype.toString = (function()
{
	var original = Date.prototype.toString;

	return function(format)
	{
		// Based on Datejs: http://www.datejs.com/
		// Formats: http://msdn.microsoft.com/en-us/library/8kb3ddd4.aspx

		var date = this;

		if(!format)
		{
			return original.call(date);
		}

		if(isNaN(this) || (date.getFullYear() === 1753 && date.getDate() === 1 && date.getMonth() === 0) || (date.getFullYear() === 9999 && date.getDate() === 1 && date.getMonth() === 0))
		{
			return '';
		}

		return format.replace(/(\\)?(dd?|MM?M?M?|yy?y?y?|hh?|HH?|mm?|ss?|fff|tt?)/g, doToken);

		function doToken(token)
		{
			if(token.charAt(0) === '\\')
			{
				return token.replace('\\', '');
			}

			switch(token)
			{
				case 'd':
					return date.getDate();
				case 'dd':
					return pad(date.getDate(), 2);
				case 'M':
					return date.getMonth() + 1;
				case 'MM':
					return pad(date.getMonth() + 1, 2);
				case 'MMM':
					return date.getMonthName().substr(0, 3);
				case 'MMMM':
					return date.getMonthName();
				case 'yy':
					return pad(date.getFullYear(), 2);
				case 'yyyy':
					return pad(date.getFullYear(), 4);
				case 'h':
					return get12Hour(date);
				case 'hh':
					return pad(get12Hour(date), 2);
				case 'H':
					return date.getHours();
				case 'HH':
					return pad(date.getHours(), 2);
				case 'm':
					return date.getMinutes();
				case 'mm':
					return pad(date.getMinutes(), 2);
				case 's':
					return date.getSeconds();
				case 'ss':
					return pad(date.getSeconds(), 2);
				case 'fff':
					return pad(date.getMilliseconds(), 3);
				case 't':
					return getAMPM(date).substr(0, 1);
				case 'tt':
					return getAMPM(date);
				default:
					return token;
			}
		}

		function get12Hour(date)
		{
			var hours = date.getHours();

			if(hours >= 13)
			{
				return hours - 12;
			}
			else
			{
				return hours === 0 ? 12 : hours;
			}
		}

		function getAMPM(date)
		{
			return date.getHours() < 12 ? 'AM' : 'PM';
		}

		function pad(string, length)
		{
			return ('000' + string).slice(-length);
		}
	};
})();

Date.prototype.getMonthName = function()
{
	return Date.monthNames[this.getMonth()];
};

Date.monthNames = ['January', 'February', 'March', 'April', 'May', 'June', 'July', 'August', 'September', 'October', 'November', 'December'];
