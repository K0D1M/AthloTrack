-- 019: «Έντυπα»: the athlete can edit the form's text itself (e.g. a Word document brought in with
-- «Εισαγωγή από Word»). Their edited copy is submitted with the answers, under the key "_document"
-- (submit_form stores the answers as given). Safe to run again.

alter table forms add column if not exists intro_editable boolean not null default false;
