﻿define(['jquery', 'utils'], function ($, utils) {
    var module = {
        init: function () {
            utils.showLoading($("#tab_panel_log"), { type: 1 });
            module.setLastCheck();

            setInterval(function () {
                module.setLastCheck();
                module.log();
            }, 5000);
        },
        log: function () {
            if ($("#tab_panel_log").is(":visible")) {
                $.getJSON('/api/logger/log', function (d) {
                    $.map(d, function (v) {
                        var $d = $("<div></div>");
                        $d.text(v);
                        if (v.indexOf("ERROR") != -1) {
                            $d.css("color","red");
                        }
                        $("#tab_panel_log .loading").before($d);
                    });
                });
            }
        },
        setLastCheck: function () {
            $("#tab_panel_log .loading span").text(" Last Check " + new Date());
        }
    };

    module.init();
});