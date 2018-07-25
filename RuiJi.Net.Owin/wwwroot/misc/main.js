﻿function aFormat(value, row, index) {
    return "<a target='_blank' href='" + value + "'>" + value + "</a>";
};
requirejs.config({
    urlArgs: 'ver=1.0.0.0',
    baseUrl: "/misc",
    waitSeconds: 0,
    paths: {
        jquery: '/scripts/jquery-3.1.1.min',
        template: '/scripts/template-web',
        bootstrap: '/scripts/bootstrap-3.3.5/dist/js/bootstrap',
        bootstrapTable: '/scripts/bootstrap-table/bootstrap-table.min',
        bootstrapDialog: '/scripts/bootstrap3-dialog/js/bootstrap-dialog',
        bootstrapEditable: '/scripts/bootstrap3-editable/js/bootstrap-editable.min',
        bootstrapTableEditable: '/scripts/bootstrap-table/extensions/editable/bootstrap-table-editable',
        flatJson: '/scripts/bootstrap-table/extensions/flat-json/bootstrap-table-flat-json',
        'tabs': '/scripts/require-tabs/require.tabs',
        'tree': '/scripts/jstree/jstree.min',
        'sweetAlert': '/scripts/sweetalert/sweetalert.min',
        'jsonViewer':'/scripts/jquery.json-viewer'
    },
    map: {
        '*': {
            'css': '/scripts/css.min.js'
        }
    },
    shim: {
        'bootstrapTableEditable': {
            deps: ['bootstrapEditable']
        },
        'bootstrapTable': {
            exports: 'bootstrapTable',
            deps: ['bootstrap', 'css!/scripts/bootstrap-table/bootstrap-table.min.css']
        },
        'bootstrapEditable': {
            deps: ['bootstrapTable', 'css!/scripts/bootstrap3-editable/css/bootstrap-editable.css']
        },
        'bootstrapDialog': {
            deps: ['bootstrap', 'css!/scripts/bootstrap3-dialog/css/bootstrap-dialog.min.css']
        },
        'tabs': {
            deps: ['css!/scripts/require-tabs/tabs.css', 'css!/fonts/font-awesome.min.css']
        },
        'tree': {
            deps: ['css!/scripts/jstree/themes/default/style.min.css', 'jquery']
        },
        'sweetAlert': {
            deps: ['css!/scripts/sweetalert/sweetalert.css']
        },
        'flatJson': {
            deps: ['bootstrapTable']
        },
        'jsonViewer': {
            deps: ['jquery', 'css!/scripts/jquery.json-viewer.css']
        }
    }
});

require(['proto']);
require(['entry']);