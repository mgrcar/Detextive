$('.token').tooltip({ html: true });
$.tablesorter.addParser({ 
    id: 'rndVar', 
    is: function(s) { 
        return s.indexOf('±') != -1; 
    }, 
    format: function(s) { 
        return s.substring(0, s.indexOf('±') - 1);
    }, 
    type: 'numeric' 
}); 
$('.tablesorter').tablesorter({ headers: { 0: { sorter: false } } });