type: fix

A paging cursor carries a sort value that contains `|` (customer identifiers allow it): the value is the last part of the payload and is read to the end, so a page boundary on such a row no longer fails with a 500.
