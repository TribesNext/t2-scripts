// TribesNext Project
// http://www.tribesnext.com/
// Copyright 2012-2013

// Tribes 2 Community System
// Browser UI Coercion

// This script implements connectivity between the Dynamix browser UI shipped with Tribes 2 and the community
// systems developed for TribesNext. The comunication to the TribesNext system via network is implemented in
// the robot client data interface script for the browser. This script merely connects (modified) Dynamix UI
// elements to query/invoke methods on this new data interface, instead of the IRC server command used initially.

// Several functional changes were made as part of this process. First, all players are now keyed by GUID, and
// all clans are now keyed by ClanID (CID). Neither clan or player names are immutable, and it is foolish to
// treat them as such. Secondly, in the initial Dynamix system, clan names were set during creation and could
// not be changed without disbanding and recreating a clan; clan names can now be changed in the new system.
// Thirdly, disbanding a clan now requires at least 50% consensus among rank 4 administrators to proceed with
// the clan disband. Fourth, all history elements for both the player and clan pages now track all major
// profile modification events, and include live links to the affected/affector player/clan profiles. These
// live links automatically reflect changes to the name and active tag of those entities. Fifth, clans are
// never fully deleted. If all members of a clan leave, or a disband consensus is reached, the clan is set
// to "inactive" status. The name and tag become available again for use by others, and it will not appear
// in searches for the clan name, but the "tombstone" of the clan can still be accessed by clicking on one
// of the "live" links in the history of any player who was a member. The tombstone will include full history,
// including who was responsible for initiating the dissolution actions. This sort of auditing should make
// players slightly more accountable, and disincentivize sabotaging a clan (since it cannot be done anonymously
// as it could in the original system). Finally, to prevent a clan from becoming leaderless, and breaking the
// ability to administer it (e.g. all rank 4 administrators leave), the browser system guarantees that any
// active clan (e.g. with at least one player) has a rank 4 by promoting an existing member. One user will be
// promoted automatically in a leaderless clan situation, and this user is decided by a combination of rank
// with join date as tie-breaker at the top rank level.

// redownload a clan profile only if at least this amount of time has passed since the last download
$TribesNext::Community::BrowserUI::MinRefreshTime = 30000;

// modify a few user interface elements to match some of the data rearrangements
function tn_community_browserui_modifyUIElements()
{
	// change the second button from "roster" to "history"
	// the clan roster will now always be visible in the player list portion
	// of the user interface -- invites are now displayed in the center
	// pane to allow more room to manage the additional invite information/options
	TL_Roster.setText("HISTORY");
	TL_Roster.setExtent(67, 27);

	// nudge the options/invites/admin buttons 2 pixels to the right
	// but defined relative to TL_Roster's position
	// note: TL_News is labeled as "OPTIONS" in the UI -- blame Dynamix
	%pos = VectorAdd(TL_Roster.getPosition(), "64 0");
	TL_News.setPosition(getWord(%pos, 0), getWord(%pos, 1));
	%pos = VectorAdd(TL_News.getPosition(), "66 0");
	TL_Invites.setPosition(getWord(%pos, 0), getWord(%pos, 1));
	%pos = VectorAdd(TL_Invites.getPosition(), "63 0");
	TW_Admin.setPosition(getWord(%pos, 0), getWord(%pos, 1));

	// set all of the buttons to the same group number
	TW_Admin.groupNum = 4;
	TL_Invites.groupNum = 4;
	TL_Profile.groupNum = 4;
	TL_Roster.groupNum = 4;
	TL_News.groupNum = 4;

	// in the tribe admin panel: edit the tag max length to 12 -- looks like Dynamix had this at 9
	TP_NewTag.maxLength = 12;
	TP_TribeTagBtn.text = "CHANGE TAG";
	// add a field to allow supporting changes to the clan name
	%nameField = new ShellTextEditCtrl();
	%nameField.maxLength = 40;
	%nameField.setPosition(0, 243);
	%nameField.setExtent(335, 64);
	ProfileControl.add(%nameField);
	TribePropertiesDlg.nameField = %nameField;
	// ... and a button to apply it
	%renameBtn = new ShellBitmapButton();
	%renameBtn.text = "RENAME";
	%renameBtn.extent = "70 38";
	%renameBtn.position = "320 243";
	%renameBtn.command = "TribePropertiesDlg.RenameTribe();";
	ProfileControl.add(%renameBtn);

	// in the member profile editor for clans
	tb_onProbation.setText("RANK 0: Probationary Member     ");
	tb_tribeMember.setText("RANK 1: Standard Member         ");
	tb_tribeAdmin.setText("RANK 2: Invitation Issuer            ");
	tb_tribeController.setText("RANK 3: Secondary Administrator");
	tb_sysAdmin.setText("RANK 4: Primary Administrator    ");
}
tn_community_browserui_modifyUIElements();

// =========================================================================
// User interface update hook to data interface.
// =========================================================================
function tn_community_browserui_clearCheckStatus()
{
	if (isEventPending($TribesNext::Community::BrowserUI::StatusSchedule))
		cancel($TribesNext::Community::BrowserUI::StatusSchedule);

	if ($TribesNext::Community::Browser::Active)
	{
		$TribesNext::Community::BrowserUI::StatusSchedule = schedule(32, 0, tn_community_browserui_clearCheckStatus);
		return;
	}
	error("Browser UI update hook occured.");

	if (TribeAndWarriorBrowserGui.searchActive)
	{
		TribeAndWarriorBrowserGui.searchActive = 0;
		tn_community_browserui_displaySearchResults();
	}
	if (TribePane.updateActive)
	{
		TribePane.updateActive = 0;
		tn_community_browserui_showTribePane();
	}
	if (PlayerPane.updateActive)
	{
		PlayerPane.updateActive = 0;
		tn_community_browserui_showPlayerPane();
	}

	// reset the cursor to non-wait mode
	Canvas.setCursor(defaultCursor);
}

// =========================================================================
// "WARRIOR SEARCH" and "TRIBE SEARCH"
// =========================================================================

// replacing function in webbrowser.cs, 618
function SearchWarriors()
{
	if(BrowserSearchPane.query !$= "player")
	{
		// clear out the fields...
		$BrowserSearchField = "";
		BrowserSearchMatchList.clear();
	}
	Canvas.pushDialog(BrowserSearchDlg);
	BrowserSearchPane.setTitle("WARRIOR SEARCH");
	BrowserSearchPane.query = "player";
	Search_EditField.makeFirstResponder(1);
}

// replacing function in webbrowser.cs, 321
function SearchTribes()
{
	if(BrowserSearchPane.query !$= "clan")
	{
		// clear out the fields...
		$BrowserSearchField = "";
		BrowserSearchMatchList.clear();
	}

	Canvas.pushDialog(BrowserSearchDlg);
	Search_EditField.makeFirstResponder(1);
	BrowserSearchPane.setTitle("TRIBE SEARCH");
	BrowserSearchPane.query = "clan";
}

// replacing function in webbrowser.cs, 57
function BrowserStartSearch()
{
	// server will reject blank searches, save a round trip by also checking here
	%search = trim(strreplace($BrowserSearchField, "%", ""));
	if(%search $="")
	{
		MessageBoxOK("NOTICE","Blank searches are not allowed; enter one or more characters of text and try again.","Search_EditField.makeFirstResponder(1);");
	}
	else
	{
		// removed the Dynamix text validation code from here; relying on client side
		// validation makes for fragile systems
		BrowserSearchPane.key = LaunchGui.key++;

		BrowserSearchMatchList.clear();
		canvas.SetCursor(ArrowWaitCursor);
		if(isEventPending(TribeAndWarriorBrowserGui.eid))
			cancel(TribeAndWarriorBrowserGui.eid);

		if(BrowserSearchPane.query $= "player")
		{
			BrowserSearchPane.state = "warriorSearch";
			tn_community_browser_user_search($BrowserSearchField);
		}
		else
		{
			BrowserSearchPane.state = "tribeSearch";
			tn_community_browser_clan_search($BrowserSearchField);
		}

		//TribeAndWarriorBrowserGui.eid = schedule(250,0,ExecuteSearch,0,BrowserSearchPane);
		TribeAndWarriorBrowserGui.searchActive = 1;
		tn_community_browserui_clearCheckStatus();
	}
}

// replacing function in webbrowser.cs, 16
function BrowserSearchDone()
{
	Canvas.popDialog(BrowserSearchDlg);
	%id = BrowserSearchMatchList.getSelectedId();
	if(%id != -1)
	{
		%row = BrowserSearchMatchList.getRowTextById(%id);
		echo(%id SPC %row);
		if(BrowserSearchPane.query $= "clan")
			TWBTabView.view(%id, %row, "Tribe");
		else
			TWBTabView.view(%id, %row);
	}
}

function tn_community_browserui_displaySearchResults()
{
	if (BrowserSearchPane.query $= "clan")
	{
		%count = $Browser::CCount;
		if (%count $= "") // no results
			return;
		for (%i = 0; %i <= %count; %i++)
		{
			%clan = $Browser::CResults[%i];
			%cid = getField(%clan, 0);
			%cname = getField(%clan, 1);

			BrowserSearchMatchList.addRow(%cid, %cname);
		}
	}
	else
	{
		%count = $Browser::PCount;
		if (%count $= "") // no results
			return;
		for (%i = 0; %i <= %count; %i++)
		{
			%player = $Browser::PResults[%i];
			%pname = getField(%player, 0);
			//%ptag = getField(%player, 1);
			//%pappend = getField(%player, 2);
			%pguid = getField(%player, 3);

			BrowserSearchMatchList.addRow(%pguid, %pname);
		}
	}
}

// =========================================================================
// Profile Viewing Utilities
// =========================================================================

// replacing function in webbrowser.cs, 683
function TribeAndWarriorBrowserGui::onWake(%this)
{   
	MemberList.ClearColumns();
	W_MemberList.ClearColumns();
	MemberList.Clear();
	W_MemberList.clear();
	Canvas.pushDialog(LaunchToolbarDlg);
	
	// DarkDragonDX: Set the BUDDYLIST and TRIBES profile
	W_BuddyList.setProfile(ShellRedRadioProfile);
	W_Tribes.setProfile(ShellRedRadioProfile);

	if (TWBTabView.tabCount() == 0)
	{
		%info = WONGetAuthInfo();

		// decode the enhanced certificate
		%cert = $T2CSRI::CommunityCertificate;
		if (getFieldCount(%cert) >= 5)
			%authInfo = getField(%cert, 4);

		%len = strlen(%authInfo);
		if (%len == 0)
			return "";
		for (%i = 0; %i < %len; %i += 2)
		{
			%byte = getSubStr(%authInfo, %i, 2);
			%char = collapseEscape("\\x" @ %byte);
			%raw = %raw @ %char;
		}

		// Open the player's page:
		%myguid = getField(WONGetAuthInfo(), 3);
		// (get current name from the enhanced cert)
		TWBTabView.view(%myguid, getField(%raw, 0));
		w_profile.setValue(1);

		// Add tabs for the player's tribal pages:
		%rcount = getRecordCount(%raw);
		for (%i = 2; %i < %rcount; %i++)
		{
			%record = getRecord(%raw, %i);
			%cid = getField(%record, 3);
			%name = getField(%record, 0);
			TWBTabView.view(%cid, %name, "Tribe");
		}

		// select the player's profile after loading clan tabs
		TWBTabView.setSelected(%myguid);

	}
	else if(PlayerPane.visible)
		PlayerPane.onWake();
	else
		TribePane.onWake();
}

// replacing function in webbrowser.cs, 995
function TWBTabView::onSelect(%this, %id, %text)
{
	%tabSet = %this.getTabSet(%id);
	%myguid = getField(WONGetAuthInfo(), 3);

	MemberList.clear();
	W_MemberList.clear();
	TWBScroll.scrollToTop();
	TWBTitle.OldText = TWBTitle.name;
	TWBTitle.setValue(%text);   // This will get overwritten...
	TWBTitle.name = %text;
	TWBClosePaneBtn.setVisible(true);
	switch(%tabSet)
	{
		case 0: // Warrior
			if(isObject(TProfileHdr))
			{
				TProfileHdr.delete();
				new GuiControl(TProfileHdr);
			}
			PlayerPane.setvisible(1);
			TribePane.setvisible(0);

			if(W_memberList.rowCount()<=0)
				PlayerPane.needRefresh = 1;
			else
				PlayerPane.needRefresh = 0;

			TWBTabFrame.setAltColor(false);

			%isMe = (%id == %myguid);

			TWBClosePaneBtn.setVisible(!%isMe);
			if(TWBTitle.OldText !$= TWBTitle.name)
				W_Profile.setValue(1);
      
			PlayerPix.setBitmap($playerGfx);
			W_Profile.setVisible(1);
			W_History.setVisible(1);
			W_Tribes.setVisible(1);

			if(%isMe)
			{
				W_BuddyList.setText("BUDDYLIST");
				W_BuddyList.setVisible(1);
				W_BuddyList.command = "PlayerPane.ButtonClick(3);";
				W_BuddyList.groupNum = 5;
			}
			else
			{
				W_BuddyList.setText("OPTIONS");
				W_BuddyList.setVisible(1);
				W_BuddyList.command = "PlayerPane.ButtonClick(4);";
				W_BuddyList.groupNum = 4;
			}
			W_Admin.setVisible(%isMe);

		case 1: // Tribe
			PlayerPane.setvisible(0);
			TribePane.setvisible(1);
			if(memberList.rowCount()<=0)
				TribePane.needRefresh = 1;
			else
				TribePane.needRefresh = 0;

			TWBTabFrame.setAltColor(true);
			if(TWBTitle.OldText !$= TWBTitle.name)
				TL_Profile.setValue(1);

			//%this.display();

	}
}

// replacing function in webbrowser.cs, 1063
function GuiMLTextCtrl::onURL(%this, %url)
{
	%i = 0;
	while((%fld[%i] = getField(%url, %i)) !$= "")
		%i++;
		
	%tribe = %fld[1];
	%warrior = %fld[2];
	switch$(%fld[0])
	{
		case "player":
			LinkBrowser( %fld[2] , "Warrior");
		case "clan": // used to be "tribe" in the Dynamix system -- it is this in TribesNext
			LaunchTabView.viewTab("BROWSER", TribeAndWarriorBrowserGui, 0);
			TWBTabView.view(%fld[1], "", "Tribe");

		case "wwwlink":
			LinkWeb( %fld[1] );

		case "retract": // TribesNext version
			%clan = tn_community_browser_getClanProfile(%fld[1]);
			%target = %fld[2];
			%player = tn_community_browser_getPlayerProfile(%target);
			%tname = %player.name;
			if (%tname $= "")
				%tname = %fld[3];

			MessageBoxYesNo("RETRACT", "Are you sure you wish to retract\n<spush><color:FFBB33>" @ %tname @ "<spop>'s invite to\n\"<spush><color:FFBB33>" @ %clan.name @ "<spop>\"?",
				"tn_community_browser_clan_retractInvite(" @ expandEscape(%fld[1]) @ ", " @ expandEscape(%target) @ ");", "");

		case "acceptinvite": // TribesNext version
			%clan = tn_community_browser_getClanProfile(%fld[1]);
			%cname = %clan.name;
			if (%cname $= "")
				%cname = %fld[2];

			MessageBoxYesNo("INVITATION", "Accept invitation to join\n\"<spush><color:FFBB33>" @ %cname @ "<spop>\"?",
				"tn_community_browser_user_acceptInvite(" @ expandEscape(%fld[1]) @ ");", "");

		case "rejectinvite": // TribesNext version
			%clan = tn_community_browser_getClanProfile(%fld[1]);
			%cname = %clan.name;
			if (%cname $= "")
				%cname = %fld[2];

			MessageBoxYesNo("INVITATION", "Are you sure you want to REJECT invitation to join\n\"<spush><color:FFBB33>" @ %cname @ "<spop>\"?",
				"tn_community_browser_user_rejectInvite(" @ expandEscape(%fld[1]) @ ");", "");

		case "email": // TribesNext version
			LinkEMail(getFields(%url, 1));

		case "invite": // TribesNext version
			%clan = tn_community_browser_getClanProfile(%fld[1]);
			%target = %fld[2];
			%player = tn_community_browser_getPlayerProfile(%target);
			%tname = %player.name;
			if (%tname $= "")
				%tname = %fld[3];

			MessageBoxYesNo("RETRACT", "Are you sure you wish to invite\n<spush><color:FFBB33>" @ %tname @ "<spop> to join\n\"<spush><color:FFBB33>" @ %clan.name @ "<spop>\"?",
				"tn_community_browser_clan_sendInvite(" @ expandEscape(%fld[1]) @ ", " @ expandEscape(%target) @ ");", "");

		case "addBuddy": // TribesNext Version
			MessageBoxYesNo("CONFIRM","Add \"" @ %fld[2] @ "\" to Buddy List?",
				"tn_community_mail_request_addListEntry(\"buddy\", \"" @ expandEscape(%fld[1]) @ "\"); PlayerPane.updateActive = 1; schedule(300, 0, tn_community_browserui_clearCheckStatus);","");
		case "delBuddy": // TribesNext Version
			MessageBoxYesNo("CONFIRM","Remove \"" @ %fld[2] @ "\" from Buddy List?",
				"tn_community_mail_request_delListEntry(\"buddy\", \"" @ expandEscape(%fld[1]) @ "\"); PlayerPane.updateActive = 1; schedule(300, 0, tn_community_browserui_clearCheckStatus);","");

		case "gamelink": // Leave this alone -- the score HUD uses this for interactivity with game servers
			commandToServer('ProcessGameLink', %fld[1], %fld[2], %fld[3], %fld[4], %fld[5]);

		case "joinPublicChat": // FUTURE Implement for TribesNext?
			joinPublicTribeChannel(getField(%url,1));
		case "joinPrivateChat": // FUTURE Implement for TribesNext?
			joinPrivateTribeChannel(getField(%url,1));

		case "activeclan": // TribesNext version
			%clan = tn_community_browser_getClanProfile(%fld[1]);
			MessageBoxYesNo("CONFIRM", "Are you sure you wish to set \n\"<spush><color:FFBB33>" @ %clan.name @ "<spop>\"\n as your active clan?",
				"tn_community_browser_user_activeClan(" @ expandEscape(%fld[1]) @ "); PlayerPane.updateActive = 1; tn_community_browserui_clearCheckStatus();","");

		case "leaveclan": // TribesNext version
			%clan = tn_community_browser_getClanProfile(%fld[1]);
			MessageBoxYesNo("CONFIRM", "Are you sure you wish to leave \n\"<spush><color:FFBB33>" @ %clan.name @ "<spop>\"?",
				"tn_community_browser_user_leaveClan(" @ expandEscape(%fld[1]) @ "); PlayerPane.updateActive = 1; tn_community_browserui_clearCheckStatus();","");

		case "emailclan": // TribesNext version
			%clan = tn_community_browser_getClanProfile(%fld[1]);
			%pcount = %clan.pcount;
			%records = "";
			if (%pcount !$= "")
			{
				for (%i = 0; %i <= %pcount; %i++)
				{
					%player = %clan.player[%i];
					%memberguid = getField(%player, 3);
					%membername = getField(%player, 0);
					%membertag = getField(%player, 1);
					%memberappend = getField(%player, 2);
					%record = %memberguid TAB %membername TAB %membertag TAB %memberappend;
					if (%memberguid != getField(WonGetAuthInfo(), 3))
						%records = %records @ "\n" @ %record;
				}
				%records = trim(%records);
			}
			LinkEMail(%records);

		//if there is an unknown URL type, treat it as a weblink..
		default:
			LinkWeb( %fld[0] );
	}
}

// =========================================================================
// Tribe Profile Viewing
// =========================================================================

// replacing function in webbrowser.cs, 960
function TWBTabView::view(%this, %id, %name, %type)
{
	if ( %type $= "Tribe" )
		%tabSet = 1;  
	else
		%tabSet = 0;  

	// see if we already have a tab with this clanid
	if (%this.getTabIndex(%id) != -1)
	{
		%this.setSelected(%id);
		return;
	}

	// Or else add the new tab:
	%this.addTab(%id, %name, %tabSet);
	%this.setSelected(%id);

	if (%tabSet == 1)
	{
		TribePane.targetid = %id;
		tn_community_browserui_showTribePane();
	}
	else
	{
		PlayerPane.targetid = %id;
		tn_community_browserui_showPlayerPane();
	}
}

// replacing function in webbrowser.cs, 1550
function TribePane::ButtonClick(%this, %senderid)
{
	canvas.SetCursor(ArrowWaitCursor);

	%tribeName = TWBTabView.getSelectedText();
	%clanid = TWBTabView.getSelectedId();

	%this.tabstate = "TRIBE";
	%this.targetid = %clanid;
	%this.state = "NONE";

	%now = getSimTime();
	if (%now < $TribesNext::Community::BrowserUI::MinRefreshTime)
		%now = ($TribesNext::Community::BrowserUI::MinRefreshTime + 1);

	switch(%senderid)
	{
		case 0: //PROFILE
			%this.state = "VIEW_CLAN";
			%clanobj = tn_community_browser_getClanProfile(%clanid);
			%delta = %now - %clanobj.lastRefresh;

			if ($TribesNext::Community::BrowserUI::MinRefreshTime < %delta)
			{
				%this.updateActive = 1;
				tn_community_browser_clan_view(%clanid);
				tn_community_browserui_clearCheckStatus();
			}
			else
			{
				tn_community_browserui_showTribePane();
			}

		case 1: //Formerly "Roster", now "History"
			%this.state = "CLAN_HISTORY";
			%clanobj = tn_community_browser_getClanProfile(%clanid);
			%delta = %now - %clanobj.lastHistRefresh;

			if ($TribesNext::Community::BrowserUI::MinRefreshTime < %delta)
			{
				%this.updateActive = 1;
				%clanobj.lastHistRefresh = %now;
				tn_community_browser_clan_history(%clanid);
				tn_community_browserui_clearCheckStatus();
			}
			else
			{
				tn_community_browserui_showTribePane();
			}

		case 2: // "OPTIONS" button -- for some reason this was called "News" in the Dynamix code
			%this.state = "CLAN_OPTIONS";
			tn_community_browserui_showTribePane();

		case 3: //INVITE BUTTON
			%this.state = "CLAN_INVITES";
			TribePane.updateActive = 1;
			tn_community_browser_clan_viewInvites(%clanid);
			tn_community_browserui_clearCheckStatus();
		case 4: //Admin Tribe
			TribePropertiesDlg.pendingChanges = "";
			Canvas.PushDialog(TribePropertiesDlg);
	}
}

// replacing function in webbrowser.cs, 1545
function TribePane::RosterDblClick(%this)
{
	LaunchBrowser(MemberList.getSelectedId(), "Warrior");
}

function tn_community_browserui_amIMember(%clanid)
{
	return (tn_community_browserui_myRankIn(%clanid) >= 0);
}

function tn_community_browserui_myRankIn(%clanid)
{
	// this pulls out data from the community certificate sent by the browser system
	%cert = $T2CSRI::CommunityCertificate;
	if (getFieldCount(%cert) >= 5)
		%authInfo = getField(%cert, 4);

	// decode the hex version of the auth info
	%len = strlen(%authInfo);
	if (%len == 0)
		return -1;
	for (%i = 0; %i < %len; %i += 2)
	{
		%byte = getSubStr(%authInfo, %i, 2);
		%char = collapseEscape("\\x" @ %byte);
		%raw = %raw @ %char;
	}

	%rcount = getRecordCount(%raw);
	for (%i = 2; %i < %rcount; %i++)
	{
		%record = getRecord(%raw, %i);
		%cid = getField(%record, 3);
		%rank = getField(%record, 4);

		if (%cid == %clanid)
			return %rank;
	}

	return -1;
}

function tn_community_browserui_showTribePane()
{
	%this = TribePane;
	%clanid = %this.targetid;
	%clan = tn_community_browser_getClanProfile(%clanid);

	%name = %clan.name;
	%tag = getField(%clan.tag, 0);
	%recru = %clan.recruiting;
	%site = %clan.site;
	%date = %clan.date;
	%pict = %clan.picture;
	%active = %clan.active;
	%info = %clan.info;

	%isMember = tn_community_browserui_amIMember(%clanid);
	%myRank = tn_community_browserui_myRankIn(%clanid);

	// hide inaccessible buttons for certain ranks
	if (%myRank < 3)
	{
		// under administrative rank
		TW_Admin.setVisible(false);
	}
	else
	{
		TW_Admin.setVisible(true);
	}

	if (%myRank < 2)
	{
		// under invitation rank
		TL_Invites.setVisible(false);
	}
	else
	{
		TL_Invites.setVisible(true);
	}

	switch$ (%this.state)
	{
		case "VIEW_CLAN":
			TWBText.clear();
			if (%name !$= "")
			{
				TWBTabView.setTabText(%clanid, %name);
			}

			// active status
			if (!%active && %name !$= "")
			{
				%activeMsg = "<spush><font:Sui Generis:14><color:cc0000>\"" @ strupr(%name) @ "\" IS INACTIVE -- THIS IS A SNAPSHOT TAKEN AT TIME OF DEACTIVATION<spop>\n";
			}
			if (%name $= "")
			{
				%activeMsg = "<spush><font:Sui Generis:14><color:00cc00>Loading...<spop>\n";
			}

			// variant of what the Dynamix system originally showed at the top of clan pages
			%Tdesc = "<lmargin:10><just:left><Font:Univers Condensed:18><color:ADFFFA>";
			%Tdesc = %Tdesc @ "Created: " @ %date @ "\n";
			%Tdesc = %Tdesc @ "Website: <a:wwwlink\t" @ %site @ ">" @ %site @ "</a>\n";
			%Tdesc = %Tdesc @ "Recruiting: <font:Univers Condensed:18>";
			%Tdesc = %Tdesc @ ((%recru && %active) ? (%isMember ? "YES" : "YES     <a:requestlink\t" @ %clanid @ ">Request Invite</a>") : "NO");
			%Tdesc = %Tdesc @ "<Font:Univers Condensed:18>" NL "<color:82BEB9><lmargin:30><Font:Univers:18>";

			// set the window title
			TWBTitle.name = %name;
			TWBTitle.setValue(%name TAB %tag);

			// show the clan picture (or default if none)
			if (%pict !$= "")
			{
				TeamPix.setBitmap(%pict);
			}
			else
				TeamPix.setBitmap("texticons/twb/twb_Lineup.jpg");

			// set the page description
			TWBText.setText(%activeMsg @ %Tdesc NL %info);

			// populate the membership roster
			MemberList.Clear();
			MemberList.ClearColumns();
			MemberList.clearList();

			MemberList.addColumn( 0, "MEMBER", 92, 0, 100,"left");
			MemberList.addColumn( 1, "TITLE", 90, 0, 100,"left");
			MemberList.addColumn( 2, "RNK", 30, 0, 40, "numeric center");

			%pcount = %clan.pcount;
			if (%pcount !$= "")
			{
				for (%i = 0; %i <= %pcount; %i++)
				{
					%member = %clan.player[%i];

					%mname = getField(%member, 0);
					%mguid = getField(%member, 3);
					%mrank = getField(%member, 4);
					%mtitle = collapseEscape(getField(%member, 5));
					%monline = getField(%member, 6);

					MemberList.addRow(%mguid, %mname TAB %mtitle TAB %mrank TAB %clanid);
					MemberList.setRowStylebyID(%mguid, !%monline);
				}
			}
		// } end case VIEW_CLAN
		case "CLAN_HISTORY":
			TWBText.clear();
			%header = "<lmargin:10><just:left><Font:Univers Condensed:18><color:ADFFFA>History:\n\n<color:82BEB9><lmargin:20><Font:Univers:18>";

			%text = "";
			%hcount = %clan.hcount;
			if (%hcount !$= "")
			{
				for (%i = %hcount; %i >= 0; %i--)
				{
					if (%clan.historyCache[%i] $= "")
					{
						%event = %clan.history[%i];
						%etype = getField(%event, 0);
						%etime = tn_community_mailui_epochToDate(getField(%event, 1));
						%payload = collapseEscape(getField(%event, 2));
						%template = getField(%event, 3);
						%player1 = getFields(%event, 4, 7);
						%player2 = getFields(%event, 8);
						%expanded = tn_community_browserui_expandTemplate(%template, %payload, %player1, %player2);
						%line = "<spush><color:ADFFFA>" @ %etime @ "<spop> " @ %expanded;

						%clan.historyCache[%i] = %line;
					}
					else
					{
						%line = %clan.historyCache[%i];
					}

					%text = %text @ %line @ "\n";
				}
			}

			TWBText.setText(%header @ %text);
		// } end case CLAN_HISTORY
		case "CLAN_OPTIONS":
			TWBText.clear();
			// this used to contain links to the clan forum, public IRC, and private IRC channels
			// -- these are not really all that useful, so, instead...
			// create link based options for a handful of TribesNext browser API calls

			// go to clan website
			// email all members of the clan
			// Set Active Clan
			// Leave Clan

			// check membership in clan before displaying all options
			%text = "<just:left><color:ADFFFA><lmargin:10><Font:Univers Condensed:18>" @ %name @ " Options:\n\n<lmargin:20>" @
				"<spush><color:ADFFCC><a:wwwlink" TAB %site @ ">Visit Website</a><spop>\n\n";
			if (%isMember)
			{
				%text = %text @ "<spush><color:ADCCFF><a:emailclan" TAB %clanid @ ">E-mail Members</a><spop>\n" @
					"<spush><color:ADCCFF><a:activeclan" TAB %clanid @ ">Set as Active Clan</a><spop>\n\n\n" @
					"<spush><color:FF8D8D><a:leaveclan" TAB %clanid @ ">Leave Clan</a><spop>";
			}
			TWBText.SetText(%text);
		// } end case CLAN_OPTIONS
		case "CLAN_INVITES":
			// since we need a few more options, this is drawn as a table in the main text area
			// not the prettiest configuration in the world, but it will do for now
			TWBText.clear();
			%header = "<lmargin:10><just:left><Font:Univers Condensed:18><color:ADFFFA>Pending Invitations:\n\n<color:82BEB9><lmargin:20><Font:Courier New:18>";

			%text = "<color:666666>";
			%icount = %clan.icount;
			if (%icount !$= "")
			{
				%sLenM = 0;
				%rLenM = 0;
				for (%i = 0; %i <= %icount; %i++)
				{
					// extract the data and determine longest values for padding calculation
					%invite = %clan.invitee[%i];
					%expiration = tn_community_mailui_epochToDate(getField(%invite, 0));
					%sender = getFields(%invite, 1, 4);
					%sLen = strlen(getField(%sender, 0)) + strlen(getField(%sender, 1));
					if (%sLen > %sLenM)
						%sLenM = %sLen;
					%recipient = getFields(%invite, 5, 8);
					%rLen = strlen(getField(%recipient, 0)) + strlen(getField(%recipient, 1));
					if (%rLen > %rLenM)
						%rLenM = %rLen;

					%line[%i, 0] = %expiration;
					%line[%i, 1] = %sender;
					%line[%i, 2] = %recipient;
				}

				// produce header and footer lines
				%separator = "+---------------------+";
				%indicator = "|  <spush><color:ADFFCC>Valid to Date/Time<spop> |";
				%indSend = "Sender";
				for (%i = 0; %i < (%sLenM + 2); %i++)
				{
					%separator = %separator @ "-";
					if (strlen(%indSend) <= %sLenM)
						%indSend = " " @ %indSend;
				}
				%separator = %separator @ "+";
				%indRec = "Recipient";
				for (%i = 0; %i < (%rLenM + 2); %i++)
				{
					%separator = %separator @ "-";
					if (strlen(%indRec) < %rLenM)
						%indRec = " " @ %indRec;
				}
				%separator = %separator @ "+---------+";

				%text = %text @ %separator @ "\n";
				%indicator = %indicator @ "<spush><color:ADFFCC>" @ %indSend @ "<spop> | <spush><color:ADFFCC>" @ %indRec @ "<spop> | <spush><color:ADFFCC>Retract<spop> |";
				%text = %text @ %indicator @ "\n";
				%text = %text @ %separator @ "\n";

				for (%i = 0; %i <= %icount; %i++)
				{
					// draw padded versions of the data in a table form
					%expiration = "<spush><color:ADFFFA>" @ %line[%i, 0] @ "<spop>";
					%sender = %line[%i, 1];
					%recipient = %line[%i, 2];

					%sPad = "";
					%rPad = "";
					%sLen = strlen(getField(%sender, 0)) + strlen(getField(%sender, 1));
					%rLen = strlen(getField(%recipient, 0)) + strlen(getField(%recipient, 1));

					while((strlen(%sPad) + %sLen) < %sLenM)
						%sPad = " " @ %sPad;
					while((strlen(%rPad) + %rLen) < %rLenM)
						%rPad = " " @ %rPad;

					%senderguid = getField(%sender, 3);
					%sender = %sPad @ tn_community_browserui_liveLinkPlayer(%sender);
					%recipientguid = getField(%recipient, 3);
					%recipient = %rPad @ tn_community_browserui_liveLinkPlayer(%recipient);

					// rank 2 users have the ability to retract invites that they send, and rank 3+'s have the ability to retract any invite to the clan
					if (%senderguid == getField(WONGetAuthInfo(), 3) || %myRank >= 3)
					{
						%retract = "<spush><color:FF6D6D><a:retract\t" @ %clanid TAB %recipientguid TAB getField(%line[%i, 2], 0) @ ">Retract</a><spop> |";
					}
					else
					{
						%retract = "        |";
					}

					%tLine = "| " @ %expiration @ " | " @ %sender @ " | " @ %recipient @ " | " @ %retract;
					%text = %text @ %tLine @ "\n";
				}
				%text = %text @ %separator @ "\n";
			}
			else
			{
				%text = "<spush><color:ADFFCC>There are no pending invites.<spop>";
			}

			TWBText.setText(%header @ %text);
	}

	// reset the cursor to non-wait mode
	Canvas.setCursor(defaultCursor);
}

// create a live link to a player with their current GUID, name, and tag
function tn_community_browserui_liveLinkPlayer(%player)
{
	// %player format: name \t tag \t append \t guid
	%name = getField(%player, 0);
	%tag = getField(%player, 1);
	%append = getField(%player, 2);
	%guid = getField(%player, 3);

	%colorhex = "";
	for (%i = 0; %i <= 2; %i++)
	{
		%byte = DecToHex(getWord($TribeTagColor, %i));
		while(strlen(%byte) < 2)
			%byte = "0" @ %byte;
		%colorhex = %colorhex @ %byte;
	}
	%tag = "<color:" @ %colorhex @ ">" @ %tag;

	%colorhex = "";
	for (%i = 0; %i <= 2; %i++)
	{
		%byte = DecToHex(getWord($PlayerNameColor, %i));
		while(strlen(%byte) < 2)
			%byte = "0" @ %byte;
		%colorhex = %colorhex @ %byte;
	}
	%name = "<color:" @ %colorhex @ ">" @ %name;

	if (%append)
		%colored = %name @ %tag;
	else
		%colored = %tag @ %name;

	return "<spush><a:player\t" @ %guid @ ">"@ %colored @ "</a><spop>";
}

function tn_community_browserui_liveLinkClan(%clan)
{
	%id = getField(%clan, 0);
	%name = getField(%clan, 1);
	// tag, append are fields 2/3 respectively, but unused here

	%colorhex = "";
	for (%i = 0; %i <= 2; %i++)
	{
		%byte = DecToHex(getWord($PlayerNameColor, %i));
		while(strlen(%byte) < 2)
			%byte = "0" @ %byte;
		%colorhex = %colorhex @ %byte;
	}
	%name = "<color:" @ %colorhex @ ">" @ %name;

	return "<spush><a:clan\t" @ %id @ ">" @ %name @ "</a><spop>";
}

// expands a template and fills in details with live links where requred
function tn_community_browserui_expandTemplate(%template, %payload, %player1, %player2, %player, %clan)
{
	%pre = "<spush><color:ADFFAD>";
	%val["@player"] = tn_community_browserui_liveLinkPlayer(%player);
	%val["@clan"] = tn_community_browserui_liveLinkClan(%clan);
	%val["@player1"] = tn_community_browserui_liveLinkPlayer(%player1);
	%val["@player2"] = tn_community_browserui_liveLinkPlayer(%player2);
	%val["@payload"] = %pre @ %payload @ "<spop>";
	%val["@payload^"] = %pre @ (%payload ? "YES" : "NO") @ "<spop>";

	%pcount = getFieldCount(%payload);
	for (%i = 0; %i < %pcount; %i++)
	{
		%field = getField(%payload, %i);
		%val["@payload;" @ %i] = %pre @ %field @ "<spop>";
		if (%field)
			%val["@payload;" @ %i @ "^"] = %pre @ "YES<spop>";
		else
			%val["@payload;" @ %i @ "^"] = %pre @ "NO<spop>";
	}

	%output = "";
	%index = strstr(%template, "@");
	while (%index != -1)
	{
		// scan ahead for a non alphanum, semicolon, or ^ character
		%strlen = strlen(%template);
		for (%i = %index; %i < %strlen; %i++)
		{
			%char = strcmp(getSubStr(%template, %i, 1), "");
			if (!((%char >= 48 && %char <= 57) || (%char >= 97 && %char <= 122) || %char == 64 || %char == 59 || %char == 94))
				break;
		}
		%lookup = getSubStr(%template, %index, %i - %index);
		%value = %val[%lookup];

		%output = %output @ getSubStr(%template, 0, %index);
		%output = %output @ %value;

		%template = getSubStr(%template, %i, %strlen);
		%index = strstr(%template, "@");
	}
	return %output @ %template;
}

// =========================================================================
// Tribe Profile Admin
// =========================================================================

// replacing function in webbrowser.cs, 2336
function TribePropertiesDlg::onWake(%this)
{
	%clanid = TribePane.targetid;
	%clan = tn_community_browser_getClanProfile(%clanid);

	%name = %clan.name;
	%tag = getField(%clan.tag, 0);
	%append = getField(%clan.tag, 1);
	%recru = %clan.recruiting;
	%info = %clan.info;

	if(%recru)
		TP_RecruitFlagBtn.setValue(1);
	else
		TP_RecruitFlagNoBtn.setValue(1);

	if(%append)
		TP_AppendFlagBtn.setValue(1);
	else
		TP_PrePendFlagBtn.setValue(1);

	TP_CurrentTag.setText(%tag);
	TP_NewTag.setText(%tag);
	TP_TribeDescription.setText(%info);

	%this.RefreshTag();
	%this.pendingChanges = "";
	Canvas.setCursor(defaultCursor);

	// add new UI elements to change the clan name
	ProfileControl.extent = "385 280";
	%this.getObject(0).setText("Tribe Administration");

	// move the close button to the top-right and make it an "X"
	TP_OKBtn.text = "X";
	TP_OKBtn.extent = "40 38";
	TP_OKBtn.position = "365 1";

	// update the clan rename field with this clan's name
	// store it so we know if we're actually changing it when the rename button is hit
	%this.nameField.setText(%name);
	%this.nameField.cname = %name;
}

// this is a new function for TribesNext
function TribePropertiesDlg::RenameTribe(%this)
{
	%field = %this.nameField;
	%clanid = TribePane.targetid;
	%org = %field.cname;
	%new = %field.getValue();
	if (%org $= %new)
	{
		MessageBoxOK("NO ACTION","Current and new name is the same.","");
	}
	else
	{
		// verify they want to do it
		MessageBoxYesNo("CONFIRM", "Are you sure you want to change the clan name from \n\"<spush><color:FFBB33>" @ %org @ "<spop>\"\nto\n\"<spush><color:FFBB33>" @ %new @ "<spop>\"?",
			"tn_community_browser_clan_rename(\"" @ expandEscape(%clanid) @ "\", \"" @ expandEscape(%new) @ "\");","");
	}
}

// replacing function in webbrowser.cs, 2371
function TribePropertiesDlg::DisbandTribe(%this)
{
	%clanid = TribePane.targetid;
	%clan = tn_community_browser_getClanProfile(%clanid);
	%name = %clan.name;

	MessageBoxYesNo("AUTHORIZE","At least 50% of Rank 4 members must authorize a disband." NL " " NL
		"Click YES to authorize, or NO to deauthorize disband of \"" @ %name @ "\".",
		"tn_community_browser_clan_disband(\"" @ expandEscape(%clanid) @ "\", 1);",
		"tn_community_browser_clan_disband(\"" @ expandEscape(%clanid) @ "\", 0);");
}

// replacing function in webbrowser.cs, 2378
function TribePropertiesDlg::ChangeRecruiting(%this)
{
	%clanid = TribePane.targetid;
	%clan = tn_community_browser_getClanProfile(%clanid);
	%recru = %clan.recruiting;

	if(TP_RecruitFlagBtn.getValue())
		%recruiting = 1;
	else
		%recruiting = 0;
	if (%recru != %recruiting)
	{
		// fire off a request to change the recruiting flag
		tn_community_browser_clan_recruiting(%clanid, %recruiting);

		%this.pendingChanges="";
	}
}

// replacing function in webbrowser.cs, 2400
function TribePropertiesDlg::ChangeTag(%this)
{
	%clanid = TribePane.targetid;
	%clan = tn_community_browser_getClanProfile(%clanid);

	if(TP_NewTag.getValue() !$= "")
	{
		%tag = TP_NewTag.getValue();
		%append = TP_AppendFlagBtn.getValue();

		// fire off request to change tag
		tn_community_browser_clan_retag(%clanid, %tag, %append);
	}
	else
	{
		MessageBoxOK("WARNING","Tribe Tag cannot be blank","TP_NewTag.makeFirstResponder(1);");
	}
}

// replacing function in webbrowser.cs, 2391
function TribePropertiesDlg::ToggleAppending(%this)
{
	%this.RefreshTag();
}

// replacing function in webbrowser.cs, 2486
function TribePropertiesDlg::setTribeGraphic(%this)
{
	%picture = TribeGraphic.bitmap;
	TeamPix.setBitmap(%picture);

	%clanid = TribePane.targetid;
	%clan = tn_community_browser_getClanProfile(%clanid);
	tn_community_browser_clan_picture(%clanid, %picture);
}

// replacing function in webbrowser.cs, 2429
function TribePropertiesDlg::ClearDescription(%this)
{
	%clanid = TribePane.targetid;
	%clan = tn_community_browser_getClanProfile(%clanid);

	MessageBoxYesNo("DESCRIPTION","Are you sure you want to clear the clan info description?",
		"tn_community_browser_clan_info(" @ %clanid @ ", \"\");TP_TribeDescription.setText(\"\");","");
}

// replacing function in webbrowser.cs, 2423
function TribePropertiesDlg::EditDescription(%this)
{
	%clanid = TribePane.targetid;
	%clan = tn_community_browser_getClanProfile(%clanid);
	%info = %clan.info;

	TWBText.editType = "tribe";
	Canvas.pushDialog(BrowserEditInfoDlg);
	EditDescriptionText.setValue(%info);
}

// replacing function in webbrowser.cs, 199
function EditDescriptionApply()
{
	%desc = EditDescriptionText.getValue();
	if(TWBText.editType $= "tribe")
	{
		%clanid = TribePane.targetid;
		%clan = tn_community_browser_getClanProfile(%clanid);
		%clan.info = %desc;

		tn_community_browser_clan_info(%clanid, %desc);
	}
	else
	{
		tn_community_browser_user_info(%desc);
		Canvas.popDialog(BrowserEditInfoDlg);

		PlayerPane.updateActive = 1;
		tn_community_browserui_clearCheckStatus();

		WP_WarriorDescription.setText(%desc);
	}
}

// replacing function in webbrowser.cs, 2187
function MemberList::onRightMouseDown(%this, %column, %row, %mousePos)
{
	MemberList.setSelectedRow(%row);
	%tguid = MemberList.getSelectedId();

	%clanid = TribePane.targetid;
	%clan = tn_community_browser_getClanProfile(%clanid);

	//echo("Right clicked on " @ %tguid @ " in clan " @ %clanid);

	TribeMemberPopup.position = %mousePos;
	Canvas.pushDialog(TribeMemberPopupDlg);
	TribeMemberPopupDlg.onWake();
	TribeMemberPopup.forceOnAction();
}

// replacing function in webbrowser.cs, 2240
function TribeMemberPopupDlg::onWake(%this)
{
	%tguid = MemberList.getSelectedId();
	%trow = MemberList.getRowTextById(%tguid);
	%tname = getField(%trow, 0);
	%ttitle = getField(%trow, 1);
	%trank = getField(%trow, 2);
	%tclanid = getField(%trow, 3);

	%myguid = getField(WONGetAuthInfo(), 3);

	TribeWarriorBrowserGui.TDialogOpen = true;
	TribeMemberPopup.clear();
	%isMember = tn_community_browserui_amIMember(%tclanid);

	TribeMemberPopup.add(%tname, -1);

	if (%tguid != %myguid)
	{
		TribeMemberPopup.add("--------------------------------------------", -1);
		TribeMemberPopup.add("Send E-mail", 2);
		TribeMemberPopup.add("Add to Buddylist", 3);
		TribeMemberPopup.add("Add to Blocklist", 4);
	}

	if(%isMember)
	{
		TribeMemberPopup.add("--------------------------------------------", -1);

		// only show the options if they can be done.
		// obviously these are all enforced on the server side too, so this is to minimize error messages shown to users
		%myRank = tn_community_browserui_myRankIn(%tclanid);

		if (%tguid == %myguid)
		{
			// targeting self
			TribeMemberPopup.add("Leave Tribe", 0); //can always leave a clan
			if (%myRank > 0) // must be at least rank 1 to change title
			{
				// anyone can downrank themselves if they so choose, however, once they are rank 1
				TribeMemberPopup.add("Edit Rank/Title", 1);
			}
			else
			{
				TribeMemberPopup.add("Too low rank to edit own title", -1);
			}
		}
		else
		{
			// targeting another user
			if (%myRank >= 3)
			{
				// see if the target rank is same/lower
				if (%trank < %myRank)
				{
					TribeMemberPopup.add("Kick from Tribe", 0);
					TribeMemberPopup.add("Edit Rank/Title", 1);
				}
				else
				{
					TribeMemberPopup.add("Too low rank to kick/edit member", -1);
				}
			}
			else
			{
				TribeMemberPopup.add("Too low rank to kick/edit member", -1);
			}
		}

	}

	Canvas.rePaint();      
}

// replacing function in webbrowser.cs, 2282
function TribeMemberPopup::onSelect(%this, %id, %text)
{
	%tguid = MemberList.getSelectedId();
	%trow = MemberList.getRowTextById(%tguid);
	%tname = getField(%trow, 0);
	%ttitle = getField(%trow, 1);
	%trank = getField(%trow, 2);
	%tclanid = getField(%trow, 3);
	%clan = tn_community_browser_getClanProfile(%tclanid);

	%myguid = getField(WONGetAuthInfo(), 3);

	switch( %id )
	{
		case 0: // Kick
			if (%tguid != %myguid)
			{
				MessageBoxYesNo("CONFIRM", "Are you sure you want to kick \"<spush><color:FFBB33>\n" @ %tname @ "<spop>\" from \"<spush><color:FFBB33>" @ %clan.name @ "<spop>\"?",
					"tn_community_browser_clan_kick(\"" @ expandEscape(%tclanid) @ "\", \"" @ expandEscape(%tguid) @ "\");", "");
			}
			else
			{
				// targeting self -- make it a leave
				MessageBoxYesNo("CONFIRM", "Are you sure that you want to leave \"<spush><color:FFBB33>" @ %clan.name @ "<spop>\"?",
					"tn_community_browser_user_leaveClan(\"" @ expandEscape(%tclanid) @ "\");", "");
			}
		case 1: // Admin Member
			LinkEditMember(%trow, TribeAdminMemberDlg);
		case 2: // EMail Member
			LinkEMail(%tguid);
		case 3: // Add To Buddylist
			MessageBoxYesNo("CONFIRM","Add \"" @ %tname @ "\" to Buddy List?",
				"tn_community_mail_request_addListEntry(\"buddy\", \"" @ expandEscape(%tguid) @ "\");","");
		case 4: // Add To Blocklist
			MessageBoxYesNo("CONFIRM","Block Email from \"" @ %tname @ "\"?",
				"tn_community_mail_request_addListEntry(\"ignore\", \"" @ expandEscape(%tguid) @ "\");","");
	}
	canvas.popDialog(TribeMemberPopupDlg);
}

// replacing function in webbrowser.cs, 389
function LinkEditMember(%row, %owner)
{
	%name = getField(%row, 0);
	%title = getField(%row, 1);
	%rank = getField(%row, 2);
	%clanid = getField(%row, 3);
	%guid = MemberList.getSelectedId();

	%clan = tn_community_browser_getClanProfile(%clanid);

	%myguid = getField(WONGetAuthInfo(), 3);
	%myRank = tn_community_browserui_myRankIn(%clanid);

	//initialize buttons
	%button[0] = tb_onProbation;
	%button[1] = tb_tribeMember;
	%button[2] = tb_tribeAdmin;
	%button[3] = tb_tribeController;
	%button[4] = tb_sysAdmin;

	for (%i = 0; %i < 5; %i++)
	{
		%button[%i].setVisible(true);
		%button[%i].setActive(false);
		%button[%i].setValue(false);
	}

	%owner.vTribe = %clanid;
	%owner.vPlayer = %guid;
	t_whois.setValue(%name);
	E_Title.setValue(%title);

	for (%i = 0; %i <= %myRank; %i++)
	{
		%button[%i].setActive(true);
	}
	%button[%rank].setValue(true);

	Canvas.pushDialog(%owner);
}

// replacing function in webbrowser.cs, 632
function SetMemberProfile()
{
	if(strLen(trim(E_Title.getValue)) <= 0)
	{
		%title = E_Title.getValue();
		%rank = TribeAdminMemberDlg.vPerm;
		%clanid = TribeAdminMemberDlg.vTribe;
		%guid = TribeAdminMemberDlg.vPlayer;

		tn_community_browser_clan_changeRank(%clanid, %guid, %rank, %title);

		Canvas.popDialog(TribeAdminMemberDlg);

		// initiate a UI update for the browser
		TribePane.updateActive = 1;
		tn_community_browserui_clearCheckStatus();
	}
	else
		MessageBoxOK("WARNING", "Member Title cannot be blank.");
}

// =========================================================================
// Warrior Profile Viewing
// =========================================================================

// replacing function in webbrowser.cs, 1864
function PlayerPane::ButtonClick(%this, %senderid)
{  
	canvas.SetCursor(ArrowWaitCursor);
	%this.tabstate = "WARRIOR";

	%myguid = getField(WONGetAuthInfo(), 3);
	%guid = TWBTabView.getSelectedId();

	%this.targetid = %guid;
	%this.state = "NONE";

	%now = getSimTime();
	if (%now < $TribesNext::Community::BrowserUI::MinRefreshTime)
		%now = $TribesNext::Community::BrowserUI::MinRefreshTime + 1;

	switch(%senderid)
	{
		case 0: // Player Profile
			%this.state = "VIEW_PLAYER";
			%playerobj = tn_community_browser_getPlayerProfile(%guid);
			%delta = %now - %playerobj.lastRefresh;

			if ($TribesNext::Community::BrowserUI::MinRefreshTime < %delta)
			{
				%this.updateActive = 1;
				tn_community_browser_user_view(%guid);
				tn_community_browserui_clearCheckStatus();
			}
			else
			{
				tn_community_browserui_showPlayerPane();
			}
		case 1:
			//Player History
                        %this.state = "PLAYER_HISTORY";
                        %playerobj = tn_community_browser_getPlayerProfile(%guid);
                        %delta = %now - %playerobj.lastHistRefresh;

                        if ($TribesNext::Community::BrowserUI::MinRefreshTime < %delta)
                        {
                                %this.updateActive = 1;
                                %playerobj.lastHistRefresh = %now;
                                tn_community_browser_user_history(%guid);
                                tn_community_browserui_clearCheckStatus();
                        }
                        else
                        {
                                tn_community_browserui_showPlayerPane();
                        }

		case 2:
			//TribeList
			%this.state = "VIEW_PLAYER";
			W_MemberList.CID = 0;
			tn_community_browserui_showPlayerPane();
		case 3:
			//Player Buddylist
			%this.state = "VIEW_PLAYER";
			W_MemberList.CID = 1;
			tn_community_browserui_showPlayerPane();
		case 4:
			//Visitor Options
			%this.state = "PLAYER_OPTIONS";
			%playerobj = tn_community_browser_getPlayerProfile(%guid);
			%delta = %now - %playerobj.lastRefresh;

			if ($TribesNext::Community::BrowserUI::MinRefreshTime < %delta)
			{
				%this.updateActive = 1;
				tn_community_browser_user_view(%guid);
				tn_community_browserui_clearCheckStatus();
			}
			else
			{
				tn_community_browserui_showPlayerPane();
			}
		case 5:
			//Admin Options
			WarriorPropertiesDlg.pendingChanges = "";
			Canvas.PushDialog(WarriorPropertiesDlg);
	}
}

function tn_community_browserui_showPlayerPane()
{
	%this = PlayerPane;
	%myguid = getField(WONGetAuthInfo(), 3);
	%guid = %this.targetid;
	%player = tn_community_browser_getPlayerProfile(%guid);

	switch$ (%this.state)
	{
		case "VIEW_PLAYER":
			if (%player.name !$= "")
			{
				%append = getField(%player.tag, 1);
				%titletag = getField(%player.tag, 0);
				TWBTitle.setText((%append ? %player.name @ %titletag : %titletag @ %player.name));
				TWBTabView.setTabText(%guid, %player.name);
			}

			W_Text.clear();

			// pretty much a straight copy of the header created by Dynamix -- just activated the previously commented out online status line
			%profileText = "<just:left><lmargin:10><color:ADFFFA><Font:Univers Condensed:10> \n<Font:Univers Condensed:18>";
			%profileText = %profileText @ "Registered:<spush><color:FFAA00>" SPC %player.date @ "<spop>\n";
			%profileText = %profileText @ "Online:        <spush>" SPC (%player.online ? "<color:33FF33>YES":"<color:FF3333>NO") @ "<spop>\n";
			%profileText = %profileText @ "Website:     " SPC "<spush><color:CCAA33><a:wwwlink\t" @ %player.site @ ">"@ %player.site @"</a><spop>\n\n";
			%profileText = %profileText @  "<color:82BEB9><Font:Univers:18><just:left><lmargin:20>";

			W_Text.setText(%profileText @ %player.info);

			if (!W_MemberList.CID)
			{
				// populate the membership data
				W_MemberList.Clear();
				W_MemberList.ClearColumns();
				W_MemberList.clearList();
				W_MemberList.addColumn( 0, "TRIBE", 94, 0, 330 );
				W_MemberList.addColumn( 1, "TITLE", 80, 0, 300 );
				W_MemberList.addColumn( 2, "RNK", 38, 0, 50, "numeric center" );

				%mcount = %player.mcount;
				if (%mcount !$= "")
				{
					%ptag = getField(%player.tag, 0);
					for (%i = 0; %i <= %mcount; %i++)
					{
						%membership = %player.membership[%i];
						%mid = getField(%membership, 0);
						%mname = getField(%membership, 1);
						%mrank = getField(%membership, 2);
						%mtitle = getField(%membership, 3);
						%mtag = getField(%membership, 4);

						W_MemberList.addRow(%mid, %mname TAB %mtitle TAB %mrank TAB %mid);
						W_MemberList.setRowStylebyID(%mid, (%mtag !$= %ptag));
					}
				}
			}
			else
			{
				W_MemberList.Clear();
				W_MemberList.ClearColumns();
				W_MemberList.clearList();
				W_MemberList.addColumn( 0, "BUDDY", 212, 0, 250 );

				// populate buddylist
				if ($TMail::ListMax["buddy"] !$= "")
				{
					%buddycount = $TMail::ListMax["buddy"];
					for (%i = 0; %i <= %buddycount; %i++)
					{
						%buddy = $TMail::ListVals["buddy", %i];
						%buddyname = getField(%buddy, 0);
						%buddyguid = getField(%buddy, 3);
						W_MemberList.addRow(%buddyguid, %buddyname);
					}
				}
			}

			// show the player picture (or default if none)
			if (%pict !$= "")
			{
				PlayerPix.setBitmap(%pict);
			}
			else
			{
				PlayerPix.schedule(300, "setBitmap", "texticons/twb/twb_Lineup.jpg");
			}
		case "PLAYER_HISTORY":
			W_Text.clear();
			%header = "<lmargin:10><just:left><Font:Univers Condensed:10>\n<Font:Univers Condensed:18><color:ADFFFA>History:\n\n<color:82BEB9><lmargin:20><Font:Univers:18>";

			%text = "";
			%hcount = %player.hcount;
			%start = getRealTime();
			if (%hcount !$= "")
			{
				for (%i = 0; %i <= %hcount; %i++)
				{
					if (%player.historyCache[%i] $= "")
					{
						%event = %player.history[%i];
						%etype = getField(%event, 0);
						%etime = tn_community_mailui_epochToDate(getField(%event, 1));
						%payload = collapseEscape(getField(%event, 2));
						%template = getField(%event, 3);
						%playerv = getFields(%event, 4, 7);
						%clan = getFields(%event, 8);
						%expanded = tn_community_browserui_expandTemplate(%template, %payload, "", "", %playerv, %clan);

						%line = "<spush><color:ADFFFA>" @ %etime @ "<spop> " @ %expanded;
						%player.historyCache[%i] = %line;
					}
					else
					{
						%line = %player.historyCache[%i];
					}
					%text = %line @ "\n" @ %text;
				}
			}
			%end = getRealTime();
			//echo("Draw Time: " @ %end - %start);
			W_Text.setText(%header @ %text);
		case "PLAYER_OPTIONS":
			W_Text.clear();
			%text = "<just:left><color:ADFFFA><lmargin:10><Font:Univers Condensed:10>\n<Font:Univers Condensed:18>Options for " @ %player.name @ ":\n\n<lmargin:20>" @
				"<spush><color:ADFFCC><a:wwwlink" TAB %player.site @ ">Visit Website</a><spop>\n";

			%text = %text @ "<spush><color:ADFFCC><a:email" TAB %guid @ ">Send E-mail</a><spop>\n";

			// check if on buddy list already and switch this to remove if so
			if (tn_community_isUserBuddy(%guid) !$= "")
				%text = %text @ "<spush><color:ADFFCC><a:delBuddy" TAB %guid TAB %player.name @ ">Remove from Buddylist</a><spop>\n";
			else
				%text = %text @ "<spush><color:ADFFCC><a:addBuddy" TAB %guid TAB %player.name @ ">Add to Buddylist</a><spop>\n";
			%text = %text @ "\n<spush><color:ADCCFF>";

			// add invitation links to clans that the current player has invitation ability to, and the target player is not a member of
			%self = tn_community_browser_getPlayerProfile(%myguid);
			if (%self.mcount !$= "")
			{
				%idxs = "";
				for (%i = 0; %i <= %self.mcount; %i++)
				{
					%membership = %self.membership[%i];
					%crank = getField(%membership, 2);
					if (%crank >= 2)
						%idxs = %idxs @ "\t" @ %i;
				}
				%idxs = trim(%idxs);
			}
			%cnt = getFieldCount(%idxs);
			for (%i = 0; %i < %cnt; %i++)
			{
				%thisidx = getField(%idxs, %i);
				%checkagainst = getField(%self.membership[%thisidx], 0);
				%found = 0;
				if (%player.mcount !$= "")
				{
					for (%j = 0; %j <= %player.mcount; %j++)
					{
						%checkid = getField(%player.membership[%j], 0);
						if (%checkagainst == %checkid)
							%found = 1;
					}
				}
				if (!%found)
					%outIdx = %outIdx @ "\t" @ %thisidx;
			}
			%outIdx = trim(%outIdx);
			%cnt = getFieldCount(%outIdx);
			for (%i = 0; %i < %cnt; %i++)
			{
				%membership = %self.membership[getField(%outIdx, %i)];
				%clanid = getField(%membership, 0);
				%clanname = getField(%membership, 1);
				%text = %text @ "<a:invite" TAB %clanid TAB %guid TAB %clanname @ ">Invite " @ %player.name @ " to join \"" @ %clanname @ "\"</a>\n";
			}

			W_Text.setText(%text);
	}

	// reset the cursor to non-wait mode
	Canvas.setCursor(defaultCursor);
}

// replacing function in webbrowser.cs, 1851
function PlayerPane::DblClick(%this)
{
	%id = W_MemberList.getSelectedId();
	%text = getField(W_MemberList.getRowTextById(%id), 0);

	%myguid = getField(WONGetAuthInfo(), 3);
	%tabid = TWBTabView.getSelectedId();

	if(w_buddylist.getValue() && (%myguid == %tabid))
	{
		TWBTabView.view(%id, %text);
	}
	else
	{
		TWBTabView.view(%id, %text, "Tribe");
	}
}

// =========================================================================
// Warrior Profile Admin
// =========================================================================

// replacing function in webbrowser.cs, 2002
function W_MemberList::onRightMouseDown( %this, %column, %row, %mousePos )
{
	%myguid = getField(WONGetAuthInfo(), 3);
	%tabid = TWBTabView.getSelectedId();

	// Open the action menu:
	W_MemberList.setSelectedRow(%row);
	if (%myguid == %tabid)
	{
		%id = W_MemberList.getSelectedId();
		%text = W_MemberList.getRowTextById(%id);
		if(w_buddylist.getValue())
		{
			// buddylist
			WarriorPopup.text = %text;
			WarriorPopup.id = %id;

			WarriorPopup.position = %mousePos;
			Canvas.pushDialog(WarriorPopupDlg);
			WarriorPopUpDlg.onWake();
			WarriorPopup.forceOnAction();
		}
		else
		{
			// clan
			WarriorPopup.text = getField(%text, 0);
			WarriorPopup.id = getField(%text, 3);
			WarriorPopup.position = %mousePos;
			Canvas.pushDialog(WarriorPopupDlg);
			WarriorPopUpDlg.onWake();
			WarriorPopup.forceOnAction();
		}
	}
}

// replacing function in webbrowser.cs, 2058
function WarriorPopupDlg::onWake( %this )
{
	%myguid = getField(WONGetAuthInfo(), 3);
	%tabid = TWBTabView.getSelectedId();

	TribeAndWarriorBrowserGui.WDialogOpen = true;
	warriorPopUP.clear();
	if (%myguid == %tabid)
	{
		switch(W_MemberList.CID)
		{
			case 0:

				WarriorPopUp.add( WarriorPopup.text, -1);
				WarriorPopUp.add( "---------------------------------------------", -1);
				WarriorPopup.add( "Clear Primary Tribe setting", 0);              
				WarriorPopUp.add( "Make Primary Tribe", 1 );
				WarriorPopup.add( "Leave Tribe", 2 );

			case 1:
				WarriorPopUp.add( WarriorPopup.text, -1);
				WarriorPopUp.add( "---------------------------------------------", -1);
				WarriorPopup.add( "Contact By EMail", 3 );
				WarriorPopup.add( "Remove from Buddylist", 4 );
				WarriorPopup.add( ".............................................", -1);
				WarriorPopup.add( "EMail BuddyList", 5 );
		}
	}
	Canvas.rePaint();
}

// replacing function in webbrowser.cs, 2092
function WarriorPopup::onSelect( %this, %id, %text )
{
	switch( %id )
	{
		case 0: // set active clan tag to none (but retain membership)
			MessageBoxYesNo("CONFIRM", "Are you sure you wish to hide your tag?",
				"tn_community_browser_user_activeClan(-1); PlayerPane.updateActive = 1; tn_community_browserui_clearCheckStatus();","");

		case 1: // set active clan tag to one of the clans the user is a member of
			MessageBoxYesNo("CONFIRM", "Are you sure you wish to set \n\"<spush><color:FFBB33>" @ %this.text @ "<spop>\"\n as your active clan?",
				"tn_community_browser_user_activeClan(" @ expandEscape(%this.id) @ "); PlayerPane.updateActive = 1; tn_community_browserui_clearCheckStatus();","");

		case 2: // leave Tribe
			MessageBoxYesNo("CONFIRM", "Are you sure you wish to leave \n\"<spush><color:FFBB33>" @ %this.text @ "<spop>\"?",
				"tn_community_browser_user_leaveClan(" @ expandEscape(%this.id) @ "); PlayerPane.updateActive = 1; tn_community_browserui_clearCheckStatus();","");
		case 3: // EMail Buddy -- FUTURE this is a little fritzy
			%record = WarriorPopup.id TAB WarriorPopup.text;
			LinkEMail(%record);
		case 4: // Remove Buddy
			MessageBoxYesNo("CONFIRM","Remove \"" @ WarriorPopup.text @ "\" from Buddy List?",
				"tn_community_mail_request_delListEntry(\"buddy\", \"" @ expandEscape(WarriorPopup.id) @ "\"); PlayerPane.updateActive = 1; schedule(300, 0, tn_community_browserui_clearCheckStatus);","");
		case 5: // TODO EMail Buddylist
			%count = w_memberlist.rowCount();
			for(%x = 0; %x < %count; %x++)
			{
				%mailList = %mailList TAB w_memberList.getRowId(%x);
			}
			%mailList = trim(%mailList);
			error(%mailList);
			//LinkEMail(%mailList);
	}
	canvas.PopDialog(WarriorPopupDlg);
}

// replacing function in webbrowser.cs, 2507
function WarriorPropertiesDlg::onWake(%this)
{
	%myguid = getField(WONGetAuthInfo(), 3);
	%player = tn_community_browser_getPlayerProfile(%myguid);

	%this.pendingChanges = "";
	UrlEdit.setValue(%player.site);
	WP_CurrentName.setValue(%player.name);
	NewNameEdit.setValue("");
	WP_WarriorDescription.setText(%player.info);
	%this.LoadGfxPane();
}

// replacing function in webbrowser.cs, 2531
function WarriorPropertiesDlg::EditDescription(%this)
{
	%myguid = getField(WONGetAuthInfo(), 3);
	%player = tn_community_browser_getPlayerProfile(%myguid);

	TWBText.editType = "player";
	Canvas.pushDialog(BrowserEditInfoDlg);
	EditDescriptionText.setValue(%player.info);
}

// replacing function in webbrowser.cs, 2538
function WarriorPropertiesDlg::ClearDescription(%this)
{
	MessageBoxYesNo("CONFIRM", "Clear your profile description?", "WarriorPropertiesDlg.doClearDescription();", "");
}

// replacing function in webbrowser.cs, 2543
function WarriorPropertiesDlg::doClearDescription(%this)
{
	%this.pendingChanges = "";
	EditDescriptionText.setText("");
	WP_WarriorDescription.setText(EditDescriptionText.getText());

	tn_community_browser_user_info("");

	PlayerPane.updateActive = 1;
	tn_community_browserui_clearCheckStatus();
}

// replacing function in webbrowser.cs, 2582
function WarriorPropertiesDlg::setPlayerGraphic(%this)
{
	PlayerPix.setBitmap(PlayerGraphic.bitmap);
	%this.pendingChanges = "";

	tn_community_browser_user_picture(PlayerGraphic.bitmap);

	PlayerPane.updateActive = 1;
	tn_community_browserui_clearCheckStatus();
}

// replacing function in webbrowser.cs, 2594
function WarriorPropertiesDlg::UpdateUrl(%this)
{
	if(trim(UrlEdit.getValue()) $= "")
	{
		UrlEdit.setValue("www.tribesnext.com");
		MessageBoxYesNo("CONFIRM","Your URL is blank, by default www.tribesnext.com will become your URL. Continue?","WarriorPropertiesDlg.setURL();","UrlEdit.setValue(\"\");");
	}
	else
		WarriorPropertiesDlg.setURL();
}

// replacing function in webbrowser.cs, 2606
function WarriorPropertiesDlg::setURL(%this)
{
	%this.pendingChanges = "";
	%url = UrlEdit.getValue();

	tn_community_browser_user_website(%url);

	PlayerPane.updateActive = 1;
	tn_community_browserui_clearCheckStatus();
}

// replacing function in webbrowser.cs, 2617
function WarriorPropertiesDlg::ChangePlayerName(%this)
{  
	MessageBoxYesNo("CONFIRM", "Are you sure you want to change your player name?", "WarriorPropertiesDlg.ProcessNameChange();", "NewNameEdit.setValue(\"\");");
}

// replacing function in webbrowser.cs, 2623
function WarriorPropertiesDlg::ProcessNameChange(%this)
{
	%this.pendingChanges = "";
	%name = NewNameEdit.getValue();

	tn_community_browser_user_rename(%name);

	PlayerPane.updateActive = 1;
	tn_community_browserui_clearCheckStatus();
}

// replacing function in webbrowser.cs, 2631
function WarriorGraphicsList::onSelect(%this)
{
	%jpg = "texticons/twb/" @ %this.getRowText(%this.getSelectedRow()) @ ".jpg";
	PlayerGraphic.setBitmap(%jpg);
}

// =========================================================================
// "CREATE TRIBE"
// =========================================================================

// replacing function in webbrowser.cs, 110
function CreateTribe()
{
	$CreateTribeName = "";
	$CreateTribeTag = "";
	$CreateTribeAppend = true;
	$CreateTribeRecruiting = true;

	if (isObject(CreateTribeDlg))
		CreateTribeDlg.delete();

	LoadGui(CreateTribeDlg);

	// modify the UI here, since it is reloaded every time it is needed
	CT_TagText.maxLength = 12; // max tag length = 12
	CT_TagText.IRCName = 0; // disables wierd/wrong tag text validation
	CreateTribeDlg.getObject(0).getObject(3).maxLength = 40; // max length of clan name
	CreateTribeDlg.getObject(0).getObject(3).IRCName = 0; // disable wierd/wrong tag name validation

	Canvas.pushDialog( CreateTribeDlg );
}

// replacing function in webbrowser.cs, 124
function CreateTribeProcess()
{
	%name = trim($CreateTribeName);
	if (strlen(%name) == 0)
	{
		MessageBoxOK("WARNING", "Tribe Name cannot be blank.");
		return;
	}
	%tag = $CreateTribeTag;
	if (strlen(%tag) == 0)
	{
		MessageBoxOK("WARNING", "Tribe Tag cannot be blank.");
		return;
	}
	%append = $CreateTribeAppend;
	%recru = $CreateTribeRecruiting;
	%info = CreateTribeDescription.getText();

	// send the creation request
	tn_community_browser_user_createClan(%tag, %append, %name, %info, %recru);
}

// New ShellRadioProfile for TRIBES and BUDDYLIST
new GuiControlProfile(ShellRedRadioProfile) {
	tab = "1";
	canKeyFocus = "1";
	modal = "1";
	opaque = "0";
	fillColor = "255 0 0 255";
	fillColorHL = "255 255 255 255";
	fillColorNA = "255 255 255 255";
	border = "0";
	borderColor = "0 0 0 255";
	borderColorHL = "0 0 0 255";
	borderColorNA = "0 0 0 255";
	fontType = "Univers Condensed";
	fontSize = "16";
	fontColors[0] = "255 0 0 255";
	fontColors[1] = "205 165 0 255";
	fontColors[2] = "5 5 5 255";
	fontColors[3] = "255 255 255 255";
	fontColors[4] = "255 255 255 255";
	fontColors[5] = "255 255 255 255";
	fontColors[6] = "255 255 255 255";
	fontColors[7] = "255 255 255 255";
	fontColors[8] = "255 255 255 255";
	fontColors[9] = "255 255 255 255";
	fontColor = "255 0 0 255";
	fontColorHL = "255 0 0 255";
	fontColorNA = "5 5 5 255";
	fontColorSEL = "255 255 255 255";
	justify = "center";
	textOffset = "0 0";
	autoSizeWidth = "0";
	autoSizeHeight = "0";
	returnTab = "0";
	numbersOnly = "0";
	cursorColor = "0 0 0 255";
	bitmap = "gui/shll_radio";
	soundButtonDown = "sButtonDown";
	soundButtonOver = "sButtonOver";

	fixedExtent = "1";
};
