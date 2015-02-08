// Tribes 2 Unofficial Authentication System
// http://www.tribesnext.com/
// Written by Thyth
// Copyright 2008-2011 by Thyth and the Tribes 2 Community System Reengineering Intitiative

// Version 1.1 initialization and glue file (server side)

// check to see if the game has been launched in offline mode
function t2csri_glue_initChecks()
{
	$t2csri::isOfflineMode = 0;
	for (%i = 0; %i < $Game::argc; %i++)
	{
		%arg = $Game::argv[%i];
		if (%arg $= "-nologin")
			$t2csri::isOfflineMode = 1;
	}
	if ($t2csri::isOfflineMode)
	{
		echo("Running TribesNext in offline mode. Not making connections to the Internet.");
	}
}
t2csri_glue_initChecks();

if (isObject(ServerGroup))
{
	// load the Ruby utils and cryptography module
	exec("t2csri/rubyUtils.cs");
	rubyExec("t2csri/crypto.rb");

	// load the torque script components
	exec("t2csri/serverSide.cs");
	exec("t2csri/serverSideClans.cs");
	exec("t2csri/bans.cs");
	exec("t2csri/ipv4.cs");
	exec("t2csri/base64.cs");

	// get the global IP for sanity testing purposes
	schedule(32, 0, ipv4_getInetAddress);
}
