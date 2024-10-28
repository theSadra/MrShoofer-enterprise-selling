/*
 * ATTENTION: The "eval" devtool has been used (maybe by default in mode: "development").
 * This devtool is neither made for production nor for readable output files.
 * It uses "eval()" calls to create a separate source file in the browser devtools.
 * If you are trying to read the output file, select a different devtool (https://webpack.js.org/configuration/devtool/)
 * or disable the default devtool with "devtool: false".
 * If you are looking for production-ready output files, see mode: "production" (https://webpack.js.org/configuration/mode/).
 */
(function webpackUniversalModuleDefinition(root, factory) {
	if(typeof exports === 'object' && typeof module === 'object')
		module.exports = factory();
	else if(typeof define === 'function' && define.amd)
		define([], factory);
	else {
		var a = factory();
		for(var i in a) (typeof exports === 'object' ? exports : root)[i] = a[i];
	}
})(self, () => {
return /******/ (() => { // webpackBootstrap
/******/ 	var __webpack_modules__ = ({

/***/ "./wwwroot/vendor/libs/jdate/calendar.js":
/*!***********************************************!*\
  !*** ./wwwroot/vendor/libs/jdate/calendar.js ***!
  \***********************************************/
/***/ (() => {

eval("/*\n JavaScript functions for the Fourmilab Calendar Converter\n\n by John Walker  --  September, MIM\n http://www.fourmilab.ch/documents/calendar/\n\n This program is in the public domain.\n */\n\n/*  MOD  --  Modulus function which works for non-integers.  */\n\nfunction mod(a, b) {\n  return a - b * Math.floor(a / b);\n}\n\n//  LEAP_GREGORIAN  --  Is a given year in the Gregorian calendar a leap year ?\n\nfunction leap_gregorian(year) {\n  return year % 4 == 0 && !(year % 100 == 0 && year % 400 != 0);\n}\n\n//  GREGORIAN_TO_JD  --  Determine Julian day number from Gregorian calendar date\n\nvar GREGORIAN_EPOCH = 1721425.5;\nfunction gregorian_to_jd(year, month, day) {\n  return GREGORIAN_EPOCH - 1 + 365 * (year - 1) + Math.floor((year - 1) / 4) + -Math.floor((year - 1) / 100) + Math.floor((year - 1) / 400) + Math.floor((367 * month - 362) / 12 + (month <= 2 ? 0 : leap_gregorian(year) ? -1 : -2) + day);\n}\n\n//  JD_TO_GREGORIAN  --  Calculate Gregorian calendar date from Julian day\n\nfunction jd_to_gregorian(jd) {\n  var wjd, depoch, quadricent, dqc, cent, dcent, quad, dquad, yindex, year, yearday, leapadj;\n  wjd = Math.floor(jd - 0.5) + 0.5;\n  depoch = wjd - GREGORIAN_EPOCH;\n  quadricent = Math.floor(depoch / 146097);\n  dqc = mod(depoch, 146097);\n  cent = Math.floor(dqc / 36524);\n  dcent = mod(dqc, 36524);\n  quad = Math.floor(dcent / 1461);\n  dquad = mod(dcent, 1461);\n  yindex = Math.floor(dquad / 365);\n  year = quadricent * 400 + cent * 100 + quad * 4 + yindex;\n  if (!(cent == 4 || yindex == 4)) {\n    year++;\n  }\n  yearday = wjd - gregorian_to_jd(year, 1, 1);\n  leapadj = wjd < gregorian_to_jd(year, 3, 1) ? 0 : leap_gregorian(year) ? 1 : 2;\n  var month = Math.floor(((yearday + leapadj) * 12 + 373) / 367),\n    day = wjd - gregorian_to_jd(year, month, 1) + 1;\n  return [year, month, day];\n}\nvar PERSIAN_EPOCH = 1948320.5;\n\n//  PERSIAN_TO_JD  --  Determine Julian day from Persian date\n\nfunction persian_to_jd(year, month, day) {\n  var epbase, epyear;\n  epbase = year - (year >= 0 ? 474 : 473);\n  epyear = 474 + mod(epbase, 2820);\n  return day + (month <= 7 ? (month - 1) * 31 : (month - 1) * 30 + 6) + Math.floor((epyear * 682 - 110) / 2816) + (epyear - 1) * 365 + Math.floor(epbase / 2820) * 1029983 + (PERSIAN_EPOCH - 1);\n}\n\n//  JD_TO_PERSIAN  --  Calculate Persian date from Julian day\n\nfunction jd_to_persian(jd) {\n  var year, month, day, depoch, cycle, cyear, ycycle, aux1, aux2, yday;\n  jd = Math.floor(jd) + 0.5;\n  depoch = jd - persian_to_jd(475, 1, 1);\n  cycle = Math.floor(depoch / 1029983);\n  cyear = mod(depoch, 1029983);\n  if (cyear == 1029982) {\n    ycycle = 2820;\n  } else {\n    aux1 = Math.floor(cyear / 366);\n    aux2 = mod(cyear, 366);\n    ycycle = Math.floor((2134 * aux1 + 2816 * aux2 + 2815) / 1028522) + aux1 + 1;\n  }\n  year = ycycle + 2820 * cycle + 474;\n  if (year <= 0) {\n    year--;\n  }\n  yday = jd - persian_to_jd(year, 1, 1) + 1;\n  month = yday <= 186 ? Math.ceil(yday / 31) : Math.ceil((yday - 6) / 30);\n  day = jd - persian_to_jd(year, month, 1) + 1;\n  return [year, month, day];\n}\n\n//# sourceURL=webpack://Vuexy/./wwwroot/vendor/libs/jdate/calendar.js?");

/***/ })

/******/ 	});
/************************************************************************/
/******/ 	
/******/ 	// startup
/******/ 	// Load entry module and return exports
/******/ 	// This entry module can't be inlined because the eval devtool is used.
/******/ 	var __webpack_exports__ = {};
/******/ 	__webpack_modules__["./wwwroot/vendor/libs/jdate/calendar.js"]();
/******/ 	
/******/ 	return __webpack_exports__;
/******/ })()
;
});