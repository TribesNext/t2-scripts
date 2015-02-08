// TribesNext Project
// http://www.tribesnext.com/
// Copyright 2011

// Tasks to be run after the login process is completed.

// load the community script components
if (WONGetAuthInfo() !$= "")
{
	exec("t2csri/community/settings.cs");
	exec("t2csri/community/login.cs");
	exec("t2csri/community/mail.cs");
	exec("t2csri/community/browser.cs");
	schedule(32, 0, exec, "t2csri/community/mailUI.cs");
	schedule(64, 0, exec, "t2csri/community/browserUI.cs");

	// log into the community server
	tn_community_login_initiate();
}