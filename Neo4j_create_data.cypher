CREATE
(clyde56:User{UserName:"clyde56", Name:"Clyde Tavares", Mail: "cukam@gmail.com", Password: "sifra1123", Salt: "posolac", Online: true}),
(johnnyz:User{UserName:"johnnyz", Name:"ohn Crittenden", Mail: "oprem@gmail.com", Password: "kkkk", Salt: "posolac", Online: true}),
(s4ra:User{UserName:"s4ra", Name:"Sarah McNair", Mail: "munem@gmail.com", Password: "zzzz", Salt: "posolac", Online: true}),

(ker:Photo{Path:"mrk1", TimePosted:datetime("2019-06-02T10:23:32.122+0100"), Description: "Jasamker"}),
(drvo:Photo{Path:"mrk2", TimePosted:datetime("2018-06-02T10:23:32.122+0100"), Description: "Drvobe"}),
(sunce:Photo{Path:"mrk3", TimePosted:datetime("2017-06-02T10:23:32.122+0100"), Description: "SUNCEE"}),
(oblak:Photo{Path:"mrk4", TimePosted:datetime("2018-06-02T10:23:32.122+0100"), Description: "OBLAKK"}),

(nature:Hashtag{Title:"Nature"}),
(sky:Hashtag{Title:"Sky"}),

(clyde56)-[:UPLOADED]->(ker),
(clyde56)-[:UPLOADED]->(drvo),
(johnnyz)-[:UPLOADED]->(sunce),
(s4ra)-[:UPLOADED]->(oblak),


(johnnyz)-[:FOLLOWS]->(s4ra),
(s4ra)-[:FOLLOWS]->(clyde56),

(drvo)-[:TAGS]->(johnnyz),

(nature)-[:HTAGS]->(drvo),
(sky)-[:HTAGS]->(oblak);


