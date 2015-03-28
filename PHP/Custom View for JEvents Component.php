<?php // components/com_jevents/views/CLAS/abstract/abstract.php

// Check to ensure this file is included in Joomla!
defined('_JEXEC') or die();

/**
 * HTML Abstract view class for the component frontend
 *
 * @static
 */
JLoader::register('JEventsDefaultView',JEV_VIEWS."/default/abstract/abstract.php");

class JEventsCLASView extends JEventsDefaultView // JEventsDefaultView is in .../views/default/abstract/abstract.php
{
	var $jevlayout = null;

	function __construct($config = null)
	{
		parent::__construct($config);

		$this->jevlayout="CLAS";	

		$this->addHelperPath(dirname(__FILE__)."/../helpers/");
	}

	function routeNavLink($date, $task)
	{
		return JRoute::_("index.php?option=".JEV_COM_COMPONENT."&task=".$task.$this->datamodel->getCatidsOutLink()."&Itemid=".JEVHelper::getItemid()."&".$date->toDateURL());
	}

	function getImagesUri()
	{
		return JURI::root()."components/".JEV_COM_COMPONENT."/views/".$this->getViewName()."/assets/images/";
	}
		
	function viewNavTableBarIconic($today_date, $this_date, $dates, $alts, $option, $task, $Itemid)
	{
		$this->writeCoreNav($today_date, $this_date, $dates, $alts, $option, $task, $Itemid);
	}

	function viewNavTableBar($today_date, $this_date, $dates, $alts, $option, $task, $Itemid)
	{
		$this->writeCoreNav($today_date, $this_date, $dates, $alts, $option, $task, $Itemid);
	}

	function calcDayOfWeek($jeDate)	// $date is a JEventDate
	{
		return idate("w", $jeDate->date);
	}
	
	function calcPositionInWeek($jeDate)	// $jeDate is a JEventDate
	{
		// Given a specific date, figures out what position (0 to 6) it should be displayed in.
		// If we start the display of weeks on Sunday (com_starday=0), then everything is simple.
		// If we start on Monday (com_starday=1), then we need to shift the day of the week down
		// by one and Sunday, normally 0 - the first day, becomes 6 - the last day.
		
		$config = & JEVConfig::getInstance();
		
		$dayOfWeek = $this->calcDayOfWeek($jeDate);	// This returns 0 for Sunday thru 6 for Saturday
		
		return ($config->get('com_starday') == 1) ? (($dayOfWeek == 0) ? 6 : ($dayOfWeek - 1)) : $dayOfWeek;
	}
	
	function calcWeekRange($jeDate, &$day0, &$day6)	// $jeDate is a JEventDate
	{
		// Given a specific date, figures out the dates for the start and end of the week containing that date.
		// Note that $day0 and $day6 are passed by reference so we can return the results in them
		
		$positionInWeek = $this->calcPositionInWeek($jeDate);
		
		$day0 = clone($jeDate);
		$day6 = clone($jeDate);
		
		$day0->addDays(-$positionInWeek);
		$day6->addDays(6-$positionInWeek);
	}

	function formatWeekRange($jeDate)	// $jeDate is a JEventDate
	{
		// Given a specific date, formats readable text for the week containing the date, eg, "March 22 - 28, 2009"
		// This will take into account what day the week should start on (com_starday) through calcPositionInWeek().
		
		$this->calcWeekRange($jeDate, $day0, $day6);
		
		if($day0->year == $day6->year)
		{
			$weekRange = JEVHelper::getMonthName($day0->month)." ".ltrim($day0->day,"0")." - ";
			
			if($day0->month == $day6->month)
			{
				$weekRange .= ltrim($day6->day,"0").", ".$day6->year;
			}
			else
			{
				$weekRange .= JEVHelper::getMonthName($day6->month)." ".ltrim($day6->day,"0").", ".$day6->year;
			}
		}
		else
		{
			$weekRange  = JEVHelper::getMonthName($day0->month)." ".ltrim($day0->day,"0").", ".$day0->year." - ";
			$weekRange .= JEVHelper::getMonthName($day6->month)." ".ltrim($day6->day,"0").", ".$day6->year;
		}
		
		return $weekRange;
	}
	
	function writeExtraNav()
	{
		$jeDate = new JEventDate();
		
		$jeDate->setDate($this->year, $this->month, $this->day );
		
		$task = JRequest::getString("jevtask");
		
		echo "<div class=\"jeExtraNav\">";
		
		echo "View by: ";
		
		if($task == "year.listevents")
		{
			echo "Year &bull; ";
		}
		else
		{
			echo "<a href=\"".$this->routeNavLink($jeDate, "year.listevents")."\">Year</a> &bull; ";
		}
		
		if($task == "month.calendar")
		{
			echo "Month &bull; ";
		}
		else
		{
			echo "<a href=\"".$this->routeNavLink($jeDate, "month.calendar")."\">Month</a> &bull; ";
		}
		
		if($task == "week.listevents")
		{
			echo "Week &bull; ";
		}
		else
		{
			echo "<a href=\"".$this->routeNavLink($jeDate, "week.listevents")."\">Week</a> &bull; ";
		}
		
		if($task == "day.listevents")
		{
			echo "Day &bull; ";
		}
		else
		{
			echo "<a href=\"".$this->routeNavLink($jeDate, "day.listevents")."\">Day</a>";
		}
		
		echo "</div>\n";
	}

	function getPrevImageTag($ToolTip)
	{
		return "<img src=\"".$this->getImagesUri()."prev.png\" width=\"16\" height=\"16\" border=\"0\" alt=\"".$ToolTip."\" />";
	}

	function getNextImageTag($ToolTip)
	{
		return "<img src=\"".$this->getImagesUri()."next.png\" width=\"16\" height=\"16\" border=\"0\" alt=\"".$ToolTip."\" />";
	}

	
	function writeCoreNav($today_date, $this_date, $dates, $alts, $option, $task, $Itemid)
	{
		$config = & JEVConfig::getInstance();
		
		$minYear = $config->get('com_earliestyear');
		$maxYear = $config->get('com_latestyear');
		
		$prevDate = $dates["prev1"];	// JEventDate
		$nextDate = $dates["next1"];	// JEventDate
		
		$prevToolTip = $alts["prev1"];	// String
		$nextToolTip = $alts["next1"];	// String
		
		$prevYear = $prevDate->year;	// Integer
		$nextYear = $nextDate->year;	// Integer
		$currYear = $this_date->year;	// Integer

		$prevMonth = $prevDate->month;	// Integer
		$nextMonth = $nextDate->month;	// Integer
		$currMonth = $this_date->month;	// Integer

		$prevDay = $prevDate->day;		// Integer
		$nextDay = $nextDate->day;		// Integer
		$currDay = $this_date->day;		// Integer
		
		$prevMonthName = JEVHelper::getMonthName($prevMonth);	// String
		$nextMonthName = JEVHelper::getMonthName($nextMonth);	// String
		$currMonthName = JEVHelper::getMonthName($currMonth);	// String
		
		$prevDisplay = false;
		$nextDisplay = false;

		if(($minYear <= $prevYear) && ($prevYear <= $maxYear))
		{
			$prevDisplay = true;
			
			switch($task)
			{
				case "year.listevents":

					$prevText = strval($prevYear);
					
					$prevToolTip .= " (".$prevText.")";

					break;
					
				case "month.calendar":

					if($prevMonth == 12)
					{
						$prevText = $prevMonthName.", ".strval($prevYear);
						
						$prevToolTip .= " (".$prevText.")";
					}
					else
					{
						$prevText= $prevMonthName;
						
						$prevToolTip .= " (".$prevText.", ".strval($prevYear).")";
					}
					
					break;
					
				case "week.listevents":

					$prevText = $prevToolTip;
					
					$prevToolTip .= " (".$this->formatWeekRange($prevDate).")";
					
					break;
					
				case "day.listevents":
					
					$prevDayName = JEVHelper::getDayName($this->calcDayOfWeek($prevDate));
					
					if(($prevMonth == 12) && ($prevDay == 31))
					{
						$prevText = $prevMonthName." ".strval($prevDay).", ".strval($prevYear);
						
						$prevToolTip .= " (".$prevDayName.", ".$prevText.")";
					}
					else
					{
						$prevText= $prevMonthName." ".strval($prevDay);
						
						$prevToolTip .= " (".$prevDayName.", ".$prevText.", ".strval($prevYear).")";
					}
					
					break;
			}
			
			$prevLink = $this->routeNavLink($prevDate, $task);
		}

		if(($minYear <= $nextYear) && ($nextYear <= $maxYear))
		{
			$nextDisplay = true;

			switch($task)
			{
				case "year.listevents":

					$nextText = strval($nextYear);
					
					$nextToolTip .= " (".$nextText.")";

					break;
					
				case "month.calendar":

					if($nextMonth == 1)
					{
						$nextText = $nextMonthName.", ".strval($nextYear);
						
						$nextToolTip .= " (".$nextText.")";
					}
					else
					{
						$nextText= $nextMonthName;
						
						$nextToolTip .= " (".$nextText.", ".strval($nextYear).")";
					}
					
					break;
					
				case "week.listevents":
					
					$nextText = $nextToolTip;
					
					$nextToolTip .= " (".$this->formatWeekRange($nextDate).")";
					
					break;
					
				case "day.listevents":
					
					$nextDayName = JEVHelper::getDayName($this->calcDayOfWeek($nextDate));

					if(($nextMonth == 1) && ($nextDay == 1))
					{
						$nextText = $nextMonthName." ".strval($nextDay).", ".strval($nextYear);
						
						$nextToolTip .= " (".$nextDayName.", ".$nextText.")";
					}
					else
					{
						$nextText= $nextMonthName." ".strval($nextDay);
						
						$nextToolTip .= " (".$nextDayName.", ".$nextText.", ".strval($nextYear).")";
					}
					
					break;
			}
			
			$nextLink = $this->routeNavLink($nextDate, $task);
		}

		switch($task)
		{
			case "year.listevents":

				$currText = strval($currYear);

				break;
				
			case "month.calendar":

				$currText = $currMonthName.", ".strval($currYear);
				
				break;
				
			case "week.listevents":
				
				$currText = $this->formatWeekRange($this_date);
				
				break;
				
			case "day.listevents":
				
				$currDayName = JEVHelper::getDayName($this->calcDayOfWeek($this_date));
				
				$currText = $currDayName.", ".$currMonthName." ".strval($currDay).", ".strval($currYear);
				
				break;
		}
		
		echo "\t<div class=\"jeCoreNav\">\n";
		
		echo "\t\t<div class=\"jeCoreNavPrev\">";
		
		if($prevDisplay)
		{
			echo "<a href=\"".$prevLink."\">".$this->getPrevImageTag($prevToolTip)."</a>&nbsp";
			echo "<a href=\"".$prevLink."\" title=\"".$prevToolTip."\" class=\"jeCoreNavLink\">".$prevText."</a>";
		}
		else
		{
			echo "&nbsp;";
		}
		
		echo "</div>\n";
		
		echo "\t\t<div class=\"jeCoreNavNext\">";
		
		if($nextDisplay)
		{
			echo "<a href=\"".$nextLink."\" title=\"$nextToolTip\" class=\"jeCoreNavLink\">".$nextText."</a>&nbsp;";
			echo "<a href=\"".$nextLink."\">".$this->getNextImageTag($nextToolTip)."</a>";
		}
		else
		{
			echo "&nbsp;";
		}
		echo "</div>\n";
		
		echo "\t\t<div class=\"jeCoreNavCurr\">".$currText."</div>\n";
		
		echo "\t</div>\n";
	}

	function formatEventDate(&$event, $includeDate=true, $includeDayOfWeek=true, $includeStartTime=true, $includeEndTime=true)
	{
		// TODO: Add year
		
		$config =& JEVConfig::getInstance();
		
		$eventStartMonth = intval($event->mup());
		$eventStartDay = intval($event->dup());
		
		$result = "";
		
		if($event->startDate() != $event->endDate())
		{
			// March 22 - March 23 - Barberton Community Clinic
			
			$eventEndMonth = intval($event->mdn());
			$eventEndDay = intval($event->ddn());
			
			if($includeDate)
			{
				$result = JEVHelper::getMonthName($eventStartMonth)." ".strval($eventStartDay)." - ".JEVHelper::getMonthName($eventEndMonth)." ".strval($eventEndDay);
			}
		}
		else
		{
			// Monday, March 22, 9:00am - 4:00pm - Barberton Community Clinic
			
			if($includeDate)
			{
				if($includeDayOfWeek)
				{
					$result = JEVHelper::getDayName($event->startWeekDay()).", ";
				}
				
				$result .= JEVHelper::getMonthName($eventStartMonth)." ".strval($eventStartDay);
			}
			
			if($includeStartTime && (!$event->alldayevent()) && ($event->starttime() != $event->endtime()))
			{
				$eventStartTime = ($config->get('com_calUseStdTime') == '1') ? date("g:ia", $event->getUnixStartTime()) : sprintf('%02d:%02d', $event->hup(), $event->minup());
				
				if($includeDate)
				{
					$result .= ', ';
				}
				
				$result .= $eventStartTime;
				
				if(($includeEndTime) && (!$event->noendtime()))
				{
					$eventEndTime	= ($config->get('com_calUseStdTime') == '1') ? date("g:ia", $event->getUnixEndTime()) : sprintf('%02d:%02d', $event->hdn(), $event->mindn());
					
					$result .= " - ".$eventEndTime;
				}
			}
		}
		
		return $result;
	}

	function writeEventListItem(&$event, $includeDate, $includeDayOfWeek, $includeStartTime, $includeEndTime, $includeIndicator, $includeContact, $includeCategory, $includePopUp, $clipTitle, $showAsImage)
	{
		$config =& JEVConfig::getInstance();

		// Get the date and time to display
		
		$eventDateTime = $this->formatEventDate($event, $includeDate, $includeDayOfWeek, $includeStartTime, $includeEndTime);
		
		$eventDateTime = JEventsHTML::special($eventDateTime);

		// Get the regular tooltip
		
		$eventToolTip = $this->formatEventDate($event)." - ".$event->title();
		
		$eventToolTip = JEventsHTML::special($eventToolTip);
		
		// Get the fancy pop-up tooltip
		
		if($includePopUp && ($config->get("com_enableToolTip",1)))
		{
			$eventPopUp = " ".$this->calendarCell_popup($event->getUnitStartTime());
		}
		else
		{
			$eventPopUp = "";
		}
		
		// Get the core content which is either the event title or the image.
		
		if($showAsImage)
		{
			$eventContent = "<img src=\"".$this->getImagesUri()."event.gif\" width=\"16\" height=\"16\" border=\"0\" alt=\"".$eventToolTip."\" align=\"left\" />";

			$eventClassExtra = " jeEventImage";
		}
		else
		{
			$eventContent = $event->title();
			
			if($clipTitle)
			{
				$maxTitleLength = $config->get('com_calCutTitle', 100);
				
				if(JString::strlen($eventContent) >= $maxTitleLength)
				{
					$eventContent = JString::substr($eventContent, 0, $maxTitleLength).'...';
				}
			}
			
			$eventContent = JEventsHTML::special($eventContent); // Converts special characters to HTML entities;
			
			$eventClassExtra = "";
		}
		
		// Get the link to the event details
		
		$eventLink = JRoute::_($event->viewDetailLink($event->yup(), $event->mup(), $event->dup(), false).$this->datamodel->getItemidLink().$this->datamodel->getCatidsOutLink());
		
		// Write out the HTML
		
		echo "\t\t\t\t\t\t<li class=\"jeEvent".$eventClassExtra."\"".$eventPopUp.">";
		
		if($includeIndicator && (!$showAsImage) && ($event->bgcolor() != "#000000"))
		{
			echo "<span class=\"jeEventIndicator\" style=\"background-color:".$event->bgcolor().";\">&nbsp;</span>";
		}
		
		if(($eventDateTime != "") && (!$showAsImage))
		{
			echo "<span class=\"jeEventDateTime\">".$eventDateTime."</span>"." - ";
		}
		
		echo "<a class=\"jeEventLink\" href=\"$eventLink\" title=\"$eventToolTip\">".$eventContent."</a>";
		
		if($includeContact && (!$showAsImage) && ($config->get("com_byview", 1)))
		{
			echo " <span class=\"jeEventContact\">".JText::_("JEV_BY")." ".$event->contactlink()."</span>";
		}
		
		if($includeCategory && (!$showAsImage))
		{
			$this->writeEventCategories($event);
		}
		
		echo "</li>\n";
	}
	
	function writeEventCategories(&$event)
	{
		$vars = JRouter::getInstance("site")->getVars();
		
		$vars["catids"] = $event->catid();
		
		$categoryLink = "index.php?";
		
		foreach($vars as $key=>$val)
		{
			$categoryLink .= $key."=".$val."&";
		}
		
		$categoryLink = substr($categoryLink, 0 ,strlen($categoryLink) - 1); // Remove the last ampersand
		
		$categoryLink = JRoute::_($categoryLink);
		
		$categoryTitle = JEventsHTML::special($event->catname()); // Converts special characters to HTML entities
		
		echo "<span class=\"jeEventCategory\">(<a href=\"".$categoryLink."\" title=\"".$categoryTitle."\">".$categoryTitle."</a>)</span>";
	}
}
