﻿define(['jquery', 'utils', 'sweetAlert'], function ($, utils) {
    var alone = false;

    var module = {
        init: function () {
            var tmp = utils.loadTemplate("/misc/setting/node.html", false);
            $("#tab_panel_setting_node").html(tmp);

            $.get("/api/setting/nodes", function (r) {

                $.map(r.crawlers, function (n) {

                    var crawler = n;
                    var baseUrl = crawler.replace("/live_nodes/crawler/", "");
                    $.getJSON("/api/zk/data?path=" + "/config/crawler/" + baseUrl, function (d) {

                        var url = "/api/crawler/ips?baseUrl=" + baseUrl;

                        $.getJSON(url, function (cips) {

                            var ips = $.parseJSON(d.data).ips;
                            var result = [];

                            $.map(cips, function (ip) {
                                var re = {};
                                if ($.inArray(ip, ips) != -1) {
                                    re = { ip: ip, checked: true };
                                } else {
                                    re = { ip: ip, checked: false };
                                }
                                result.push(re);
                            })

                            tmp = utils.template("setting_crawler_node", { crawler: baseUrl, result: result });
                            $("#crawler_node_set").append(tmp);
                        });
                    });
                });

                $.map(r.feeds, function (n) {

                    var feed = n.replace("/live_nodes/feed/", "");
                    var url = "/api/feed/get?baseUrl=" + feed;

                    $.getJSON(url, function (pages) {
                        tmp = utils.template("setting_feed_node", { feed: feed, pages: pages });
                        $("#feed_node_set").append(tmp);
                    });
                });

            });

            $(document).on("click", ".save-ips", function () {
                var result = $(this).closest(".crawler-node");

                var ips = result.find(".tab-panel-node:checked").map(function () {
                    return $(this).val()
                }).get();

                var crawler = result.find(".active-node-crawler").text();
                var url = "/api/crawler/ips?baseUrl=" + crawler;

                $.ajax({
                    url: url,
                    data: JSON.stringify(ips),
                    type: 'POST',
                    contentType: "application/json",
                    success: function (res) {
                        swal("success", "The ips have been set!", "success");
                    }
                });
            });

            $(document).on("click", ".save-feed", function () {
                var allPages = $.map($(".feed-node .node-pages"), function (n) { return $(n).val(); }).join(",");;
                var reg = new RegExp(/([^,]+)[\s\S]*\1/).exec(allPages);
                if (reg) {
                    swal("repeat page", "the page " + reg[1] + " repeat,the feed node can't have a repeat page", "warning");
                    return;
                }
                var result = $(this).closest(".feed-node");

                var pages = result.find(".node-pages").val();

                var feed = result.find(".active-node-feed").text();

                var url = "/api/feed/set?baseUrl=" + feed;

                $.ajax({
                    url: url,
                    data: JSON.stringify(pages),
                    type: 'POST',
                    contentType: "application/json",
                    success: function (res) {
                        swal("success", "The feed have been set!", "success");
                    }
                });
            });

        }
    };

    $.getJSON("/api/alone", function (d) {
        alone = d;

        module.init();
    });
});