// TribesNext Project
// http://www.tribesnext.com/
// Copyright 2011-2013

// Tribes 2 Community System
// Robot Mail Client

// This script implements a network data interface to the TribesNext community system mail robot data interface.
// The "robot" data interface provides the data in a way that is easy to parse with the meager and medicore
// string processing and parsing features present in the Tribes 2 game. If you are reading this script and desire
// to make some sort of third party client for web access or other purposes, you will have a much easier time
// if you use the JSON API to access the same data.

// Currently available methods (as of RC3) are as follow:
//  - Viewing the inbox.
//  - Viewing the sentbox.
//  - Viewing the deleted messages box.
//  - Viewing messages.
//  - Viewing ignore list.
//  - Viewing buddy list.
//  - Adding users to an ignore list.
//  - Adding users to a buddy list.
//  - Deleting users from an ignore list.
//  - Deleting users from a buddy list.
//  - Deleting (and undeleting) messages.
//  - Getting a message count (both read and unread).
//  - Sending messages.

// Since the API is asynchronous, this interface will cache results to the various inboxes and viewed
// messages for the purposes of display. Temporary data (elipses) will be provided to the drawing code
// until all fields are filled in.

$TribesNext::Community::Mail::Active = 0;
$TribesNext::Community::Mail::ChunkSize = 25;

function CommunityMailInterface::onConnected(%this)
{
	echo("Sending: " @ %this.data);
	%this.primed = 0;
	%this.send(%this.data);
}

function CommunityMailInterface::onDisconnect(%this)
{
	$TribesNext::Community::Mail::Active = 0;
	tn_community_mail_executeNextRequest();
}

function CommunityMailInterface::onLine(%this, %line)
{
	if (trim(%line) $= "")
	{
		%this.primed = 1;
		
		tn_community_mail_requestCompleted();
		return;
	}
	if (!%this.primed)
		return;

	warn("mail: " @ %line);
	%message = getField(%line, 0);
	switch$ (%message)
	{
		// display errors to the user -- some of these should never actually happen
		case "ERR":
			if (getField(%line, 1) $= "MAIL")
			{
				%type = getField(%line, 2);
				switch$ (%type)
				{
					case "INVALID_RECIP":
						%message = "Invalid recipient in mail send request.";
					case "INVALID_SBJ":
						%message = "Blank or invalid subject in mail send request.";
					case "INVALID_BODY":
						%message = "Blank or invalid body in message send request.";
					case "UNAUTHENTICATED":
						%message = "Session authentication error in mail request.";
					case "NO_METHOD":
						%message = "Internal error: no mail method specified in request.";
					case "UNKNOWN_METHOD":
						%message = "Internal error: unknown mail method specified in request.";
					case "READ":
						%message = "Access denied on message ID #" @ getField(%line, 3) @ ".";
					default:
						%message = "Unknown error in mail system: " @ %line;
				}
				schedule(500, 0, MessageBoxOK, "ERROR", %message);
			}
		// success is sent when a message is sent out
		case "SUCCESS":
			schedule(500, 0, MessageBoxOK, "SENT", "Your message has been sent.");

		// the rest of these should be handled and accepted quietly to populate the various data objects

		// message format sent as part of a box search
		case "MSG":
			%msg = tn_community_mail_getMessageObject(getField(%line, 1));
			%msg.box = getField(%line, 2);
			%msg.read = getField(%line, 3);
			%msg.type = getField(%line, 4);
			%msg.time = getField(%line, 5);

			%box = tn_community_mail_getMailboxObject(%msg.box);
			if (!%box.isMember(%msg))
			{
				if (%box.newest < %msg.id)
					%box.newest = %msg.id;
				%box.add(%msg);
			}

			// check if we're getting new messages
			if (%box.gettingNew)
			{
				%since = %box.since;
				if (%msg.id <= %since)
				{
					// found the desired message
					%box.gettingNew = 0;
					%box.since = %box.newest;
				}
				else
				{
					// not yet found desired message, try the next chunk

					// first make sure that the chunk exists and we're not at the end of the mailbox
					%box.chunk = %box.chunk + 1;
					if ($TMail::MessageBoxCount[%box.name] > (%box.chunk * $TribesNext::Community::Mail::ChunkSize))
						tn_community_mail_request_boxList(%box.chunk * $TribesNext::Community::Mail::ChunkSize, (%box.chunk + 1) * $TribesNext::Community::Mail::ChunkSize, %box.name, %since);
					else
					{
						%box.since = %box.newest;
					}
				}
			}
		// message format sent as part of a message view
		case "MSG2":
			%msg = tn_community_mail_getMessageObject(getField(%line, 1));
			%msg.deleted = getField(%line, 2);
			%msg.type = getField(%line, 3);
			%msg.time = getField(%line, 4);
			%msg.read = "true";
		// message subject
		case "SBJ":
			tn_community_mail_getMessageObject(getField(%line, 1)).subject = getField(%line, 2);
		// sender of a message
		case "SNDR":
			tn_community_mail_getMessageObject(getField(%line, 1)).sender = tn_community_util_extractPlayer(%line, 2);
		// body of a message
		case "BDY":
			tn_community_mail_getMessageObject(getField(%line, 1)).body = collapseEscape(getField(%line, 2));
		// "to" recipient of a message
		case "TO":
			%msg = tn_community_mail_getMessageObject(getField(%line, 1));
			%index = getField(%line, 2);
			%msg.to[%index] = tn_community_util_extractPlayer(%line, 3);
			if (%msg.toMax < %index)
				%msg.toMax = %index;
		// "cc" recipient of a message
		case "CC":
			%msg = tn_community_mail_getMessageObject(getField(%line, 1));
			%index = getField(%line, 2);
			%msg.cc[%index] = tn_community_util_extractPlayer(%line, 3);
			if (%msg.ccMax < %index)
				%msg.ccMax = %index;
		// entries of a buddy or ignore list
		case "LIST":
			$TMail::ListVals[getField(%line, 1), getField(%line, 2)] = tn_community_util_extractPlayer(%line, 3);
			if ($TMail::ListMax[getField(%line, 1)] < getField(%line, 2))
				$TMail::ListMax[getField(%line, 1)] = getField(%line, 2);
		// search results for player name queries
		case "SEARCH":
			$TMail::SearchVals[getField(%line, 2)] = tn_community_util_extractPlayer(%line, 3);
			if ($TMail::SearchMax < getField(%line, 2))
				$TMail::SearchMax = getField(%line, 2);
		// unread message count for a box
		case "COUNT_U":
			$TMail::MessageBoxUnread[getField(%line, 1)] = getField(%line, 2);
		// message count for a box
		case "COUNT_A":
			$TMail::MessageBoxCount[getField(%line, 1)] = getField(%line, 2);
	}
}

// extract four fields from a string that correspond to a player
function tn_community_util_extractPlayer(%string, %fInit)
{
	return getField(%string, %fInit) @ "\t" @ getField(%string, %fInit + 1) @ "\t" @ getField(%string, %fInit + 2) @ "\t" @ getField(%string, %fInit + 3);
}

function tn_community_mail_getMessageObject(%id)
{
	if (isObject($TMail::MessageTable[%id]))
		return $TMail::MessageTable[%id];

	%obj = new SimObject()
	{
		class = TMailMessage;
		id = %id;
	};
	$TMail::MessageTable[%id] = %obj;

	$TMailMessageSet.add(%obj);
	return %obj;
}

function tn_community_mail_getMailboxObject(%name)
{
	if (isObject($TMail::MailboxTable[%name]))
		return $TMail::MailboxTable[%name];

	%obj = new SimSet()
	{
		class = TMailBox;
		name = %name;
		since = 0;
	};
	$TMail::MailboxTable[%name] = %obj;
	return %obj;
}

function tn_community_mail_initMessageSet()
{
	if (isObject($TMailMessageSet))
	{
		while ($TMailMessageSet.getCount() > 0)
			$TMailMessageSet.getObject(0).delete();
		$TMailMessageSet.delete();
	}
	$TMailMessageSet = new SimSet("TMailMessageSet");
}
tn_community_mail_initMessageSet();

function tn_community_mail_initQueue()
{
	if (isObject($TMailRequestQueue))
		$TMailRequestQueue.delete();
	$TMailRequestQueue = new MessageVector();
}
tn_community_mail_initQueue();

function tn_community_mail_processRequest(%request, %payload)
{
	if (%request !$= "")
	{
		%request = "?guid=" @ getField($LoginCertificate, 1) @ "&uuid=" @ $TribesNext::Community::UUID @ "&" @  %request;
	}
	if (%payload $= "")
	{
		%data = "GET " @ $TribesNext::Community::BaseURL @ $TribesNext::Community::MailScript @ %request;
		%data = %data @ " HTTP/1.1\r\nHost: " @ $TribesNext::Community::Host @ "\r\nUser-Agent: Tribes 2\r\nConnection: close\r\n\r\n";
	}
	else
	{
		%data = "POST " @ $TribesNext::Community::BaseURL @ $TribesNext::Community::MailScript @ " HTTP/1.1\r\n";
		%data = %data @ "Host: " @ $TribesNext::Community::Host @ "\r\nUser-Agent: Tribes 2\r\nConnection: close\r\n";
		%data = %data @ %payload;
	}

	$TMailRequestQueue.pushBackLine(%data);

	if (!$TribesNext::Community::Mail::Active)
		tn_community_mail_executeNextRequest();
}

function tn_community_mail_executeNextRequest()
{
	if ($TMailRequestQueue.getNumLines() <= 0)
		return;

	%data = $TMailRequestQueue.getLineText(0);
	$TMailRequestQueue.popFrontLine();

	$TribesNext::Community::Mail::Active = 1;

	if (isObject(CommunityMailInterface))
	{
		CommunityMailInterface.disconnect();
	}
	else
	{
		new TCPObject(CommunityMailInterface);
	}
	CommunityMailInterface.data = %data;
	CommunityMailInterface.connect($TribesNext::Community::Host @ ":" @ $TribesNext::Community::Port);
}

// implementation of API requests

// this isn't strictly an API request -- this gets the latest messages since the last check
function tn_community_mail_request_getNew(%box)
{
	%obj = tn_community_mail_getMailboxObject(%box);
	tn_community_mail_request_count(%box, "all");
	%since = %obj.since;
	%obj.gettingNew = 1;
	%obj.chunk = 0;
	tn_community_mail_request_boxList(0, $TribesNext::Community::Mail::ChunkSize, %box, %since);
}

function tn_community_mail_request_boxList(%first, %last, %box, %since)
{
	tn_community_mail_processRequest("method=box&first=" @ %first @ "&last=" @ %last @ "&box=" @ %box @ "&since=" @ %since);
}

function tn_community_mail_request_read(%messageId)
{
	tn_community_mail_processRequest("method=read&id=" @ %messageId);
}

function tn_community_mail_request_viewList(%list)
{
	$TMail::ListMax[%list] = 0;
	deleteVariables("$TMail::ListVals" @ %list @ "*");
	tn_community_mail_processRequest("method=viewlist&list=" @ %list);
}

function tn_community_mail_request_addListEntry(%list, %target)
{
	tn_community_mail_processRequest("method=addlist&list=" @ %list @ "&target=" @ %target);
	tn_community_mail_request_viewList(%list); // refresh the list
}

function tn_community_mail_request_delListEntry(%list, %target)
{
	tn_community_mail_processRequest("method=dellist&list=" @ %list @ "&target=" @ %target);
	tn_community_mail_request_viewList(%list); // refresh the list
}

function tn_community_mail_request_deleteMessage(%messageId, %set)
{
	%msg = tn_community_mail_getMessageObject(%messageId);
	if (%set $= "0")
	{
		%add = "&set=0";
		%msg.deleted = "false";
	}
	else
	{
		%add = "&set=1";
		%msg.deleted = "true";
	}
	tn_community_mail_processRequest("method=delete&id=" @ %messageId @ %add);
	tn_community_mail_request_read(%messageId); // refresh the message status

	// move the message to the right box
	if (%set !$= "0")
	{
		// been deleted, make sure it's in the deleted set
		%box = tn_community_mail_getMailboxObject(%msg.box);
		%box.remove(%msg);
		tn_community_mail_getMailboxObject("deleted").add(%msg);
		%msg.box = "deleted";
	}
	else
	{
		// been undeleted? make sure it's not in the deleted set
		tn_community_mail_getMailboxObject("deleted").remove(%msg);
		if (getField(%msg.sender, 3) !$= getField($LoginCertificate, 1))
			%box = tn_community_mail_getMailboxObject("inbox");
		else
			%box = tn_community_mail_getMailboxObject("sentbox");
		%box.add(%msg);
		%msg.box = %box.name;
	}
}

function tn_community_mail_request_count(%box, %mode)
{
	tn_community_mail_processRequest("method=count&box=" @ %box @ "&mode=" @ %mode);
}

function tn_community_mail_request_search(%query)
{
	$TMail::SearchMax = 0;
	deleteVariables("$TMail::SearchVals*");
	tn_community_mail_processRequest("method=search&query=" @ %query);
}

function tn_community_mail_request_send(%subject, %contents, %to, %cc)
{
	// sending messages themselves is done with a POST,
	// since the contents can be longer than URI length limits
	%guid = getField($LoginCertificate, 1);
	%uuid = $TribesNext::Community::UUID;

	%boundary = "-------------------------";
	%rand = getRandom(10000, 99999) @ getRandom(10000, 99999) @ getRandom(10, 9999);
	%formelem = "Content-Disposition: form-data; name=\"";

	%payload = "--" @ %boundary @ %rand @ "\r\n";

	// GUID element
	%payload = %payload @ %formelem @ "guid\"\r\n\r\n" @ %guid @ "\r\n";
	%payload = %payload @ "--" @ %boundary @ %rand @ "\r\n";

	// UUID
	%payload = %payload @ %formelem @ "uuid\"\r\n\r\n" @ %uuid @ "\r\n";
	%payload = %payload @ "--" @ %boundary @ %rand @ "\r\n";

	// method
	%payload = %payload @ %formelem @ "method\"\r\n\r\nsend\r\n";
	%payload = %payload @ "--" @ %boundary @ %rand @ "\r\n";

	// subject
	%payload = %payload @ %formelem @ "subject\"\r\n\r\n" @ %subject @ "\r\n";
	%payload = %payload @ "--" @ %boundary @ %rand @ "\r\n";

	// contents
	%payload = %payload @ %formelem @ "contents\"\r\n\r\n" @ %contents @ "\r\n";
	%payload = %payload @ "--" @ %boundary @ %rand @ "\r\n";

	// to
	%payload = %payload @ %formelem @ "to\"\r\n\r\n" @ %to @ "\r\n";
	%payload = %payload @ "--" @ %boundary @ %rand @ "\r\n";

	// cc
	if (trim(%cc) $= "")
		%cc = 0; // DarkDragonDX: No CC?
		
	%payload = %payload @ %formelem @ "cc\"\r\n\r\n" @ %cc @ "\r\n";
	%payload = %payload @ "--" @ %boundary @ %rand @ "\r\n";

	%header = "Content-Type: multipart/form-data; boundary=" @ %boundary @ %rand @ "\r\n";
	%header = %header @ "Content-Length: " @ strlen(%payload) @ "\r\n\r\n";

	tn_community_mail_processRequest("", %header @ %payload);
}

function tn_community_isOnList(%searchguid, %list)
{
	if ($TMail::ListMax[%list] $= "")
		return "";
	%count = $TMail::ListMax[%list];
	for (%i = 0; %i <= %count; %i++)
	{
		%player = $TMail::ListVals[%list, %i];
		%guid = getField(%player, 3);
		if (%guid == %searchguid)
			return %player;
	}
	return "";
}

function tn_community_isUserBuddy(%searchguid)
{
	return tn_community_isOnList(%searchguid, "buddy");
}

function tn_community_isUserBlocked(%searchguid)
{
	return tn_community_isOnList(%searchguid, "ignore");
}

// DarkDragonDX: Hookable script callback for when a request with the mail system completes
function tn_community_mail_requestCompleted(){ }

// DarkDragonDX: Helpers function to work with the JSON (somewhat)
function tn_community_mail_explodeJSONObject(%json)
{
	%json = trim(%json);
	%json = stripChars(%json, "{}\"'");
	// The EMail contents of a tribal invite shouldn't contain spaces so this should be safe
	%json = strReplace(%json, ",", " ");
	
	return %json;
}

// %processed should have been processed with tn_community_mail_explodeJSONObject
function tn_community_mail_getJSONElement(%processed, %element)
{
	%element = strlwr(%element);
	
	for (%i = 0; %i < getWordCount(%processed); %i++)
	{
		%word = strReplace(getWord(%processed, %i), ":", " ");
		if (strlwr(getWord(%word, 0)) $= %element)
			return getWord(%word, 1);
	}
	
	return -1;
}
