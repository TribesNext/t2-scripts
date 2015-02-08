// TribesNext Project
// http://www.tribesnext.com/
// Copyright 2011-2012

// Tribes 2 Community System
// Robot Browser Client

// This scripts implements a network data interface to the TribseNext community system browser robot data
// interface. The "robot" data interface provides the data in a way that is easy to parse with the meager
// and mediocre string processing and parsing features present in the Tribes 2 game. If you are reading this
// script and desire to make some sort of third party client for web access or other purposes, you will have
// a much easier time if you use the JSON API to access the same data.

// Currently available methods (as of RC3) are as follow:
//  - Retrieval of a "Community Enhanced Certificate" indicating current name/tag/membership.
//  - Search for clans by name.
//  - Search for players by name.
//  - View a clan profile.
//  - View a player profile.
//  - View a clan history.
//  - View a player history.
//  - Change recruiting status of a clan.
//  - Change the profile info of a clan.
//  - Change a clan's tag.
//  - Change a clan's website.
//  - Change a clan name.
//  - Change which in-game picture is displayed on the clan profile page.
//  - Invite a player to a clan.
//  - View outstanding invites to a clan.
//  - Change the rank/title of either one's self, or of others in a clan.
//  - Authorize disbanding a clan.
//  - Kick another player from a clan.
//  - Change the player name of an account.
//  - Change which clan tag an account will display.
//  - Change a player's profile website.
//  - Change a player's profile info.
//  - Accept an invitation to join a clan.
//  - Reject an invitation to join a clan.
//  - Leave a clan.
//  - Create a clan.

$TribesNext::Community::Browser::Active = 0;

function CommunityBrowserInterface::onConnected(%this)
{
	echo("Browser-Sending: " @ %this.data);
	%this.primed = 0;
	%this.send(%this.data);
}

function CommunityBrowserInterface::onDisconnect(%this)
{
	if (!%this.primed)
	{
		// nothing sent from the server
		// this means there is probably a firewall interfering with the communication
		// with a rare chance that the browser system is unavailable (in which case we will post an announcement)
		// alert the user the first time
		if (!$TribesNext::Community::Browser::Firewalled)
		{
			$TribesNext::Community::Browser::Firewalled = 1;
			schedule(500, 0, MessageBoxOK, "NETWORK", "Unable to communicate with the browser server via HTTP. Reconfigure your firewall to allow access.");
		}
	}
	$TribesNext::Community::Browser::Active = 0;
	tn_community_Browser_executeNextRequest();
}

function CommunityBrowserInterface::onLine(%this, %line)
{
	if (trim(%line) $= "")
	{
		%this.primed = 1;
		return;
	}
	if (!%this.primed)
		return;

	warn("Browser: " @ %line);

	if (getSubStr(%line, 0, 4) $= "ERR:")
	{
		// A really exceptional error happened in the browser system
		schedule(500, 0, MessageBoxOK, "SYSTEM ERROR", trim(getSubStr(%line, 4, strlen(%line))));
	}

	%message = getField(%line, 0);
	switch$ (%message)
	{
		// display errors to the user -- most of these should never actually happen
		// unless the user is being intentionally naughty or is operating on stale data
		case "ERR":
			if (getField(%line, 1) $= "BROWSER")
			{
				schedule(500, 0, MessageBoxOK, "ERROR", getFields(%line, 2));
			}

		// DCE and CEC returns are certificates that are handed to game servers to get tags
		// and handle name changes -- they replace the authInfo field, instead of having the
		// server generate a skeleton version of the authInfo from the GUID and name.
		// in the absense of a DCE/CEC pair, the player will still be able to play with the
		// "raw" account, using the original account name and no tag
		case "DCE":
			%dceCert = collapseEscape(getField(%line, 1));
			%index = getField(%dceCert, 1);
			$T2CSRI::ClientDCESupport::DCECert[%index] = %dceCert;
		case "CEC":
			$T2CSRI::CommunityCertificate = collapseEscape(getField(%line, 1));
			// schedule a refresh
			%expire = getField($T2CSRI::CommunityCertificate, 2);
			rubyEval("tsEval '$temp=\"' + (" @ %expire @ " - Time.now().to_i).to_s + '\";'");
			%expire = $temp - 60;
			if (%expire > 0)
			{
				if (isEventPending($TribesNext::Browser::CertRefreshSch))
					cancel($TribesNext::Browser::CertRefreshSch);
				$TribesNext::Browser::CertRefreshSch = schedule(1000 * %expire, 0, tn_community_Browser_request_cert);
			}
			else
			{
				schedule(500, 0, MessageBoxOK, "ERROR", "Received expired certificate from community server. Is your computer's clock set correctly?");
			}
			
			// DarkDragonDX: We seem to have received the community certificate, enable the browser and EMail UI's
			for ( %i = 0; %i < LaunchTabView.tabCount(); %i++ )
			{
				%guiName = LaunchTabView.gui[%i];
				if (isObject(%guiName) && (%guiName $="EmailGui" || %guiName $= "TribeandWarriorBrowserGui"))
					LaunchTabView.setTabActive(%i, true);
			}

		// data access methods

		// results for searching for a clan by name
		case "CSEARCH":
			%query = getField(%line, 1);
			if ($Browser::CQuery !$= %query)
			{
				// new query -- wipe old results
				deleteVariables("$Browser::CResults*");
				$Browser::CQuery = %query;
				$Browser::CCount = "";
			}

			%idx = getField(%line, 2) + 0;
			%answer = getFields(%line, 3);
			$Browser::CResults[%idx] = %answer;
			if (%idx >= $Browser::CCount)
				$Browser::CCount = %idx;

		// results for querying a clan (all necessary to display in UI)
		case "CLAN":
			%id = getField(%line, 2);
			%var = getField(%line, 1);

			%clan = tn_community_browser_getClanProfile(%id);
			if (%var $= "NAME")
			{
				// wipe the players so membership doesn't look screwed up if it shrinks
				tn_community_browser_wipePlayers(%clan);
				%clan.name = getField(%line, 3);
				%clan.lastRefresh = getSimTime();
			}
			else if (%var $= "TAG")
			{
				%clan.tag = getField(%line, 3) TAB getField(%line, 4);
			}
			else if (%var $= "RECR")
			{
				%clan.recruiting = getField(%line, 3);
			}
			else if (%var $= "SITE")
			{
				%clan.site = getField(%line, 3);
			}
			else if (%var $= "DATE")
			{
				// convert epoch to human readable
				%clan.date = tn_community_mailui_epochToDate(getField(%line, 3));
			}
			else if (%var $= "PICT")
			{
				%clan.picture = getField(%line, 3);
			}
			else if (%var $= "ACTIVE")
			{
				%clan.active = getField(%line, 3);
			}
			else if (%var $= "INFO")
			{
				%clan.info = collapseEscape(getField(%line, 3));
			}
			else if (%var $= "MEMB")
			{
				%idx = getField(%line, 3) + 0;
				%clan.player[%idx] = getFields(%line, 4);

				if (%idx >= %clan.pcount)
					%clan.pcount = %idx;
			}

		// results for querying a clan history
		case "CHIST":
			%id = getField(%line, 1);
			%idx = getField(%line, 2) + 0;

			%clan = tn_community_browser_getClanProfile(%id);
			%event = getFields(%line, 3);
			if (%event !$= %clan.history[%idx])
			{
				// underlying event has changed, clear the display cache
				// this may occur if one of the underlying players has changed
				// active clan, or name
				%clan.historyCache[%idx] = "";
			}
			%clan.history[%idx] = %event;

			if (%idx >= %clan.hcount)
				%clan.hcount = %idx;

		// results for searching for a player by name
		case "SEARCH":
			%query = getField(%line, 1);
			if ($Browser::PQuery !$= %query)
			{
				// new query -- wipe old results
				deleteVariables("$Browser::PResults*");
				$Browser::PQuery = %query;
				$Browser::PCount = "";
			}

			%idx = getField(%line, 2) + 0;
			%answer = tn_community_util_extractPlayer(%line, 3);
			$Browser::PResults[%idx] = %answer;
			if ($Browser::PCount $= "" || %idx >= $Browser::PCount)
				$Browser::PCount = %idx;

		// results for querying a player (all necessary to display in UI)
		case "PLAYER":
			%guid = getField(%line, 2);
			%var = getField(%line, 1);

			%player = tn_community_browser_getPlayerProfile(%guid);

			if (%var $= "NAME")
			{
				tn_community_browser_wipeMemberships(%player);
				%player.name = getField(%line, 3);
				%player.lastRefresh = getSimTime();
			}
			else if (%var $= "TAG")
			{
				%player.tag = getField(%line, 3) TAB getField(%line, 4);
			}
			else if (%var $= "DATE")
			{
				// convert epoch to human readable
				%player.date = tn_community_mailui_epochToDate(getField(%line, 3));
			}
			else if (%var $= "SITE")
			{
				%player.site = getField(%line, 3);
			}
			else if (%var $= "INFO")
			{
				%player.info = collapseEscape(getField(%line, 3));
			}
			else if (%var $= "ONLINE")
			{
				%player.online = getField(%line, 3);
			}
			else if (%var $= "CLAN")
			{
				%idx = getField(%line, 3) + 0;
				%player.membership[%idx] = getFields(%line, 4);

				if (%idx >= %player.mcount)
					%player.mcount = %idx;
			}

		// results for querying a player history
		case "PHIST":
			%guid = getField(%line, 1);
			%idx = getField(%line, 2) + 0;

			%player = tn_community_browser_getPlayerProfile(%guid);
			%event = getFields(%line, 3);
			if (%event !$= %player.history[%idx])
			{
				// underlying event has changed, clear the display cache
				// this may occur if one of the underlying players has changed
				// active clan, or name
				%player.historyCache[%idx] = "";
			}
			%player.history[%idx] = %event;

			if (%idx >= %player.hcount)
				%player.hcount = %idx;

		// clan management

		// clan recruit flag changes
		case "CLAN_RECRUITING":
			// for these clan management methods, we just queue a redownload of the profile page
			%id = getField(%line, 2);

			tn_community_browser_getClanProfile(%id).lastRefresh = 0;
			tn_community_browser_clan_view(%id);

		// clan info page changes
		case "CLAN_INFO":
			// for these clan management methods, we just queue a redownload of the profile page
			%id = getField(%line, 2);

			tn_community_browser_getClanProfile(%id).lastRefresh = 0;
			tn_community_browser_clan_view(%id);

		// clan tag changes
		case "CLAN_RETAGGED":
			// for these clan management methods, we just queue a redownload of the profile page
			%id = getField(%line, 1);

			tn_community_browser_getClanProfile(%id).lastRefresh = 0;
			tn_community_browser_clan_view(%id);

		// clan website changes
		case "CLAN_WEBSITE":
			// for these clan management methods, we just queue a redownload of the profile page
			%id = getField(%line, 1);

			tn_community_browser_getClanProfile(%id).lastRefresh = 0;
			tn_community_browser_clan_view(%id);

		// clan renamed
		case "CLAN_RENAMED":
			// for these clan management methods, we just queue a redownload of the profile page
			%id = getField(%line, 1);

			tn_community_browser_getClanProfile(%id).lastRefresh = 0;
			tn_community_browser_clan_view(%id);

		// clan team picture changed
		case "CLAN_TEAMPIC":
			// for these clan management methods, we just queue a redownload of the profile page
			%id = getField(%line, 2);

			tn_community_browser_getClanProfile(%id).lastRefresh = 0;
			tn_community_browser_clan_view(%id);

		// clan invitation sent
		case "INVITED":
			%id = getField(%line, 2);
			%guid = getField(%line, 1);
			%clan = tn_community_browser_getClanProfile(%id);
			%player = tn_community_browser_getPlayerProfile(%guid);

			if (%clan.name !$= "" && %player.name !$= "")
			{
				// have both clan and player name
				%out = "Sent \"" @ %player.name @ "\" an invitation to join \"" @ %clan.name @ "\".";
			}
			else if (%clan.name $= "" && %player.name !$= "")
			{
				// just have player name
				%out = "Sent invitation to \"" @ %player.name @ "\".";
			}
			else if (%clan.name !$= "" && %player.name $= "")
			{
				// just have clan name
				%out = "Sent invitation to join \"" @ %clan.name @ "\".";
			}
			else
			{
				// have neither clan or player name
				%out = "Invitation sent.";
			}
			schedule(500, 0, MessageBoxOK, "INVITATION", %out);

		// results for querying the pending invites on a clan
		case "INVITEE":
			%id = getField(%line, 1);
			%idx = getField(%line, 2) + 0;

			%clan = tn_community_browser_getClanProfile(%id);
			%clan.invitee[%idx] = getFields(%line, 3);

			if (%idx > %clan.icount)
				%clan.icount = %idx;


		// changing a clan rank/title of one's self
		case "CLAN_RANK_SET":
			// for these clan management methods, we just queue a redownload of the profile page
			%id = getField(%line, 1);

			tn_community_browser_getClanProfile(%id).lastRefresh = 0;
			tn_community_browser_clan_view(%id);

		// changing a clan rank/title of another clan member
		case "CLAN_RANK_OTHER":
			// for these clan management methods, we just queue a redownload of the profile page
			%id = getField(%line, 1);

			tn_community_browser_getClanProfile(%id).lastRefresh = 0;
			tn_community_browser_clan_view(%id);

		// retracting a disband authorization
		case "DISBAND_RETRACTED":
			%id = getField(%line, 1);
			%clan = tn_community_browser_getClanProfile(%id);
			if (%clan.name !$= "")
			{
				%out = "You have retracted your disband authorization for \"" @ %clan.name @ "\".";
			}
			else
			{
				%out = "You have retracted your disband authorization for the clan.";
			}
			schedule(500, 0, MessageBoxOK, "AUTHORIZATION", %out);

		// clan disband authorized, but still need disband consensus
		case "AUTHORIZED_DISBAND":
			%id = getField(%line, 1);
			%clan = tn_community_browser_getClanProfile(%id);
			if (%clan.name !$= "")
			{
				%out = "You have authorized disbanding \"" @ %clan.name @ "\".";
			}
			else
			{
				%out = "You have authorized disbanding the clan.";
			}
			schedule(500, 0, MessageBoxOK, "AUTHORIZATION", %out);

		// clan disband authorized and disband completed
		case "DISBANDED_CLAN":
			%id = getField(%line, 1);
			%clan = tn_community_browser_getClanProfile(%id);
			if (%clan.name !$= "")
			{
				%out = "You have disbanded clan \"" @ %clan.name @ "\".";
			}
			else
			{
				%out = "You have disbanded the clan.";
			}

			schedule(500, 0, MessageBoxOK, "DISBANDED", %out);

			tn_community_browser_clan_view(%id);

		// kicked another person from clan
		case "CLAN_KICKED":
			// for these clan management methods, we just queue a redownload of the profile page
			%id = getField(%line, 1);

			tn_community_browser_getClanProfile(%id).lastRefresh = 0;
			tn_community_browser_clan_view(%id);

		// user profile management

		// user successfully renamed the visible name of their account
		case "RENAMED":
			// update our own profile and certificate
			tn_community_browser_user_view(getField($LoginCertificate, 1));
			tn_community_browser_request_cert();

		// user switched their active clan tag (or turned it off)
		case "ACTIVE_CLAN":
			// update our own profile and certificate
			tn_community_browser_user_view(getField($LoginCertificate, 1));
			tn_community_browser_request_cert();

		// user switched their profile website link
		case "PLAYER_WEBSITE":
			// update our own profile
			tn_community_browser_user_view(getField($LoginCertificate, 1));

		// user switched their profile info
		case "PLAYER_INFO_UPDATED":
			// update our own profile
			tn_community_browser_user_view(getField($LoginCertificate, 1));

		// player accepted an invitation to join a clan
		case "CLAN_JOINED":
			// update our copy of the clan profile
			%id = getField(%line, 1);
			tn_community_browser_getClanProfile(%id).lastRefresh = 0;
			tn_community_browser_clan_view(%id);
			// also update our own profile and certificate
			tn_community_browser_user_view(getField($LoginCertificate, 1));
			tn_community_browser_request_cert();

		// player has rejected an invitation to join a clan
		case "INVITATION_DECLINED":
			%id = getField(%line, 1);
			%clan = tn_community_browser_getClanProfile(%id);
			if (%clan.name !$= "")
			{
				%out = "You have rejected an invitation to join \"" @ %clan.name @ "\".";
			}
			else
			{
				%out = "You have rejected this clan invitation.";
			}

			schedule(500, 0, MessageBoxOK, "INVITATION", %out);

		// player has left a clan
		case "CLAN_LEFT":
			// update our copy of the clan profile
			%id = getField(%line, 1);
			tn_community_browser_getClanProfile(%id).lastRefresh = 0;
			tn_community_browser_clan_view(%id);
			// also update our own profile and certificate
			tn_community_browser_user_view(getField($LoginCertificate, 1));
			tn_community_browser_request_cert();

		// player has created a clan
		case "CLAN_CREATED":
			// update our copy of the clan profile
			%id = getField(%line, 1);
			tn_community_browser_getClanProfile(%id).lastRefresh = 0;
			tn_community_browser_clan_view(%id);
			// also update our own profile and certificate
			tn_community_browser_user_view(getField($LoginCertificate, 1));
			tn_community_browser_request_cert();

			// UI hook: pop the creation dialog
			Canvas.popDialog(CreateTribeDlg);
			// for the newly created clan, create a new tab switch us to it
			LaunchTabView.viewTab("BROWSER", TribeAndWarriorBrowserGui, 0);
			TWBTabView.view(%id, "", "Tribe");
	}
}

// primary data store structures for the browser

// BrowserPlayer:
//    guid (primary key)
//    name
//    tag = tag \t append
//    date (epoch)
//    site
//    info
//    online (boolean)
//    mcount
//    membership[x] = clanid \t clanname \t rank \t title \t tag \t append
//    hcount
//    history[x] = (player history entry) -> entry 0 is oldest

// BrowserClan:
//    id (primary key)
//    name
//    tag = (tag \t append)
//    recruiting (boolean)
//    site
//    date (epoch)
//    picture
//    active (boolean)
//    info
//    pcount
//    player[x] = (name \t tag \t append \t guid) \t rank \t escape(title) \t online?
//    hcount
//    history[x] = (clan history entry) -> entry 0 is oldest
//    icount
//    invitee[x] = expiration \t (sender player) \t (recipient player)

function tn_community_browser_wipeMemberships(%this)
{
	%count = %this.mcount;
	for (%i = 0; %i < %count; %i++)
	{
		%this.membership[%i] = "";
	}
	%this.mcount = "";

	%count = %this.icount;
	for (%i = 0; %i < %count; %i++)
	{
		%this.invitee[%i] = "";
	}
	%this.icount = "";
}

function tn_community_browser_wipePlayers(%this)
{
	%count = %this.pcount;
	for (%i = 0; %i < %count; %i++)
	{
		%this.player[%i] = "";
	}
	%this.pcount = "";
}

function tn_community_browser_getPlayerProfile(%guid)
{
	if (isObject($Browser::PlayerProfileTable[%guid]))
		return $Browser::PlayerProfileTable[%guid];

	%profile = new SimObject()
	{
		classname = BrowserPlayer;
		guid = %guid;
	};
	$Browser::PlayerProfileTable[%guid] = %profile;

	$BrowserPlayerSet.add(%profile);
	return %profile;
}

function tn_community_browser_getClanProfile(%id)
{
	if (isObject($Browser::ClanProfileTable[%id]))
		return $Browser::ClanProfileTable[%id];

	%profile = new SimObject()
	{
		classname = BrowserClan;
		id = %id;
	};
	$Browser::ClanProfileTable[%id] = %profile;

	$BrowserClanSet.add(%profile);
	return %profile;
}

function tn_community_browser_initQueue()
{
	// initialize a message vector to handle queuing requests to the remote system
	if (isObject($BrowserRequestQueue))
		$BrowserRequestQueue.delete();
	$BrowserRequestQueue = new MessageVector();

	// initialize the browser player and clan object caches
	if (isObject($BrowserPlayerSet))
	{
		while ($BrowserPlayerSet.getCount() > 0)
			$BrowserPlayerSet.getObject(0).delete();
		$BrowserPlayerSet.delete();
	}
	if (isObject($BrowserClanSet))
	{
		while ($BrowserClanSet.getCount() > 0)
			$BrowserClanSet.getObject(0).delete();
		$BrowserClanSet.delete();
	}
	$BrowserPlayerSet = new SimSet();
	$BrowserClanSet = new SimSet();
}
tn_community_browser_initQueue();

function tn_community_browser_processRequest(%request, %payload)
{
	if (%request !$= "")
	{
		%request = "?guid=" @ getField($LoginCertificate, 1) @ "&uuid=" @ $TribesNext::Community::UUID @ "&" @  %request;
	}
	if (%payload $= "")
	{
		%data = "GET " @ $TribesNext::Community::BaseURL @ $TribesNext::Community::BrowserScript @ %request;
		%data = %data @ " HTTP/1.1\r\nHost: " @ $TribesNext::Community::Host @ "\r\nUser-Agent: Tribes 2\r\nConnection: close\r\n\r\n";
	}
	else
	{
		%data = "POST " @ $TribesNext::Community::BaseURL @ $TribesNext::Community::BrowserScript @ " HTTP/1.1\r\n";
		%data = %data @ "Host: " @ $TribesNext::Community::Host @ "\r\nUser-Agent: Tribes 2\r\nConnection: close\r\n";
		%data = %data @ %payload;
	}

	$BrowserRequestQueue.pushBackLine(%data);

	if (!$TribesNext::Community::Browser::Active)
		tn_community_browser_executeNextRequest();
}

function tn_community_browser_executeNextRequest()
{
	if ($BrowserRequestQueue.getNumLines() <= 0)
		return;

	%data = $BrowserRequestQueue.getLineText(0);
	$BrowserRequestQueue.popFrontLine();

	$TribesNext::Community::Browser::Active = 1;

	if (isObject(CommunityBrowserInterface))
	{
		CommunityBrowserInterface.disconnect();
	}
	else
	{
		new TCPObject(CommunityBrowserInterface);
	}
	CommunityBrowserInterface.data = %data;
	CommunityBrowserInterface.connect($TribesNext::Community::Host @ ":" @ $TribesNext::Community::Port);
}


// URL escape a string
function tn_community_browser_urlescape(%string)
{
	// this function transforms all characters into %xx form, regardless of whether
	// it is necessary to actually encode them
	%out = "";
	%len = strlen(%string);
	for (%i = 0; %i < %len; %i++)
	{
		%c = getSubStr(%string, %i, 1);
		%hex = DecToHex(strCmp(%c, ""));
		while (strlen(%hex) < 2)
			%hex = "0" @ %hex;
		%out = %out @ "%" @ %hex;
	}

	return %out;
}

// implementation of API requests

function tn_community_browser_request_cert()
{
	error("Browser: Downloading enhanced certificate from community server.");
	tn_community_browser_processRequest("method=cert");
}

// * * * clan data accessor API methods (these do not require authentication)

// search for clans by name, returns a list of full-names and clan ID numbers
function tn_community_browser_clan_search(%name)
{
	$Browser::CCount = "";
	%name = tn_community_browser_urlescape(%name);
	tn_community_browser_processRequest("method=csearch&a0=" @ %name);
}

// views clan information for a given clan (e.g. memberships, info, website, recruiting, etc.)
function tn_community_browser_clan_view(%id)
{
	tn_community_browser_processRequest("method=cview&a0=" @ %id);
}

// views clan history (e.g. renamings, retaggings, invitations, kickings, ranking)
function tn_community_browser_clan_history(%id)
{
	tn_community_browser_processRequest("method=chist&a0=" @ %id);
}

// * * * clan management API methods (these require authentication, membership, and sufficient rank)

// set the recruiting yes/no flag on the clan (alteration rank)
function tn_community_browser_clan_recruiting(%id, %set)
{
	tn_community_browser_processRequest("method=crecr&a0=" @ %id @ "&a1=" @ %set);
}

// set the clan info field string (alteration rank)
function tn_community_browser_clan_info(%id, %info)
{
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
	%payload = %payload @ %formelem @ "method\"\r\n\r\ncinfo\r\n";
	%payload = %payload @ "--" @ %boundary @ %rand @ "\r\n";

	// id
	%payload = %payload @ %formelem @ "a0\"\r\n\r\n" @ %id @ "\r\n";
	%payload = %payload @ "--" @ %boundary @ %rand @ "\r\n";

	// info
	%payload = %payload @ %formelem @ "a1\"\r\n\r\n" @ %info @ "\r\n";
	%payload = %payload @ "--" @ %boundary @ %rand @ "\r\n";

	%header = "Content-Type: multipart/form-data; boundary=" @ %boundary @ %rand @ "\r\n";
	%header = %header @ "Content-Length: " @ strlen(%payload) @ "\r\n\r\n";

	tn_community_browser_processRequest("", %header @ %payload);
}

// set the clan tag, and whether it is prepended or appended to player names (alteration rank)
function tn_community_browser_clan_retag(%id, %tag, %append)
{
	%tag = tn_community_browser_urlescape(%tag);
	tn_community_browser_processRequest("method=ctag&a0=" @ %id @ "&a1=" @ %tag @ "&a2=" @ %append);
}

// set the clan website string (alteration rank)
function tn_community_browser_clan_website(%id, %website)
{
	%website = tn_community_browser_urlescape(%website);
	tn_community_browser_processRequest("method=csite&a0=" @ %id @ "&a1=" @ %website);
}

// set the clan name string (alteration rank)
function tn_community_browser_clan_rename(%id, %name)
{
	%name = tn_community_browser_urlescape(%name);
	tn_community_browser_processRequest("method=cname&a0=" @ %id @ "&a1=" @ %name);
}

// set the filename of the displayed clan picture in the in-game browser (alteration rank)
function tn_community_browser_clan_picture(%id, %picture)
{
	%picture = tn_community_browser_urlescape(%picture);
	tn_community_browser_processRequest("method=cpict&a0=" @ %id @ "&a1=" @ %picture);
}

// invite another player (by GUID) to this clan (recruitment rank)
function tn_community_browser_clan_sendInvite(%id, %invitee)
{
	tn_community_browser_processRequest("method=cinvite&a0=" @ %id @ "&a1=" @ %invitee);
}

// cancel another player's (by GUID) invitation to this clan (recruitment rank, for self invites -- admin for any invite)
function tn_community_browser_clan_retractInvite(%id, %invitee)
{
	tn_community_browser_processRequest("method=cretract&a0=" @ %id @ "&a1=" @ %invitee);
}

// set a list of outstanding invitations for a clan (recruitment rank)
function tn_community_browser_clan_viewInvites(%id)
{
	tn_community_browser_processRequest("method=cinvview&a0=" @ %id);
}

// change the rank/title of a user in the clan
// if invoked on self, this works so long as the member is not of probation rank
// if invoked on others, this requires alteration rank -- it can raise others to the user's rank
//   but, it cannot be used to reduce the rank of someone at the same (or greater) level
function tn_community_browser_clan_changeRank(%id, %target, %rank, %title)
{
	%title = tn_community_browser_urlescape(%title);
	tn_community_browser_processRequest("method=crank&a0=" @ %id @ "&a1=" @ %target @ "&a2=" @ %rank @ "&a3=" @ %title);
}

// sets authorization status for a clan disband
// must be administrative rank, and must be consensus of at least 50% to complete the disband
// authorization can be de-set if an administrative rank user changes their mind
// if 50% of administrative rank users authorize disband, the disband will happen instantly
// when serving this request
function tn_community_browser_clan_disband(%id, %set)
{
	tn_community_browser_processRequest("method=cdisb&a0=" @ %id @ "&a1=" @ %set);
}


// kick a user from the clan
// requires alteration rank, but cannot kick users at same (or greater) level
function tn_community_browser_clan_kick(%id, %target)
{
	tn_community_browser_processRequest("method=ckick&a0=" @ %id @ "&a1=" @ %target);
}

// * * * user data accessor API methods (do not require authentication)

// search for players by name -- returns full matching names and GUIDs
function tn_community_browser_user_search(%name)
{
	%name = tn_community_browser_urlescape(%name);
	$Browser::PCount = "";
	tn_community_browser_processRequest("method=usearch&a0=" @ %name);
}

// view info for a user -- profile, clan memberships, registration date, etc.
function tn_community_browser_user_view(%guid)
{
	tn_community_browser_processRequest("method=uview&a0=" @ %guid);
}

// view history for a user -- clan creations/joins/leavings/kicks/kicked/disband/renamings
function tn_community_browser_user_history(%guid)
{
	tn_community_browser_processRequest("method=uhist&a0=" @ %guid);
}

// * * * user profile management API methods (require authentication as the player him/herself)

// request a name-change, this will be fulfilled it if is unused for raw accounts, unused
// in a prior rename browser-side, and deemed acceptable to the existing naming policy
function tn_community_browser_user_rename(%name)
{
	%name = tn_community_browser_urlescape(%name);
	tn_community_browser_processRequest("method=uname&a0=" @ %name);
}

// sets the active displayed clan to the given ID. By setting -1, it is possible to retain
// clan memberships, but not show a tag (this was not possible in the Dynamix browser)
function tn_community_browser_user_activeClan(%id)
{
	tn_community_browser_processRequest("method=uclan&a0=" @ %id);
}

// set profile website link
function tn_community_browser_user_website(%website)
{
	%website = tn_community_browser_urlescape(%website);
	tn_community_browser_processRequest("method=usite&a0=" @ %website);
}

// set the filename of the displayed user picture in the in-game browser
function tn_community_browser_user_picture(%picture)
{
	%picture = tn_community_browser_urlescape(%picture);
	tn_community_browser_processRequest("method=upict&a0=" @ %picture);
}

// set info string to given data
function tn_community_browser_user_info(%info)
{
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
	%payload = %payload @ %formelem @ "method\"\r\n\r\nuinfo\r\n";
	%payload = %payload @ "--" @ %boundary @ %rand @ "\r\n";

	// info
	%payload = %payload @ %formelem @ "a0\"\r\n\r\n" @ %info @ "\r\n";
	%payload = %payload @ "--" @ %boundary @ %rand @ "\r\n";

	%header = "Content-Type: multipart/form-data; boundary=" @ %boundary @ %rand @ "\r\n";
	%header = %header @ "Content-Length: " @ strlen(%payload) @ "\r\n\r\n";

	tn_community_browser_processRequest("", %header @ %payload);
}

// accept an invitation to the specified clan ID -- errors if no such invite exists or is invalid
// can also produce an error if the user is in the maximum number of clans (but will not burn the
// invite in the process -- can retry after leaving an existing clan)
function tn_community_browser_user_acceptInvite(%id)
{
	tn_community_browser_processRequest("method=uaccept&a0=" @ %id);
}

// ditto to above, but rejects the invite instead
function tn_community_browser_user_rejectInvite(%id)
{
	tn_community_browser_processRequest("method=ureject&a0=" @ %id);
}

// leave a clan -- this will always succeed provided the user was a member of it.
// if the user was actively using this clan's tag, and they are still a member of at least one
// clan, the active tag will be set to the clan that the user has been a member of the longest
function tn_community_browser_user_leaveClan(%id)
{
	tn_community_browser_processRequest("method=uleave&a0=" @ %id);
}

// create a clan -- specify the tag/name/info
function tn_community_browser_user_createClan(%tag, %append, %name, %info, %recruiting)
{
	// do as POST since the contents can be longer than URI length limits
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
	%payload = %payload @ %formelem @ "method\"\r\n\r\ncreate\r\n";
	%payload = %payload @ "--" @ %boundary @ %rand @ "\r\n";

	// tag
	%payload = %payload @ %formelem @ "a0\"\r\n\r\n" @ %tag @ "\r\n";
	%payload = %payload @ "--" @ %boundary @ %rand @ "\r\n";

	// append
	%payload = %payload @ %formelem @ "a1\"\r\n\r\n" @ %append @ "\r\n";
	%payload = %payload @ "--" @ %boundary @ %rand @ "\r\n";

	// name
	%payload = %payload @ %formelem @ "a2\"\r\n\r\n" @ %name @ "\r\n";
	%payload = %payload @ "--" @ %boundary @ %rand @ "\r\n";

	// info
	%payload = %payload @ %formelem @ "a3\"\r\n\r\n" @ %info @ "\r\n";
	%payload = %payload @ "--" @ %boundary @ %rand @ "\r\n";

	// append
	%payload = %payload @ %formelem @ "a4\"\r\n\r\n" @ %recruiting @ "\r\n";
	%payload = %payload @ "--" @ %boundary @ %rand @ "\r\n";

	%header = "Content-Type: multipart/form-data; boundary=" @ %boundary @ %rand @ "\r\n";
	%header = %header @ "Content-Length: " @ strlen(%payload) @ "\r\n\r\n";

	tn_community_browser_processRequest("", %header @ %payload);
}

// DarkDragonDX: Removed this from being a schedule; it does it when it's ready now
// schedule(3000, 0, tn_community_Browser_request_cert);
