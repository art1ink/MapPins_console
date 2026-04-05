SLASH_COMMANDS["/data"]=function()
	SavedVars.MapIdMap ={}
	for i=1,10000,1 do
		local mapTexure = GetMapTileTextureForMapId(i)
		if mapTexure ~= "" then		
			SavedVars.MapIdMap[mapTexure:match("[^\\/]+$"):lower():gsub("%.dds$", ""):gsub("_[0-9]+$", "")] = i
		end
	end
end