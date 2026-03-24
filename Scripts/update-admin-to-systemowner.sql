-- Temporary: Update admin user to SystemOwner role for demo
UPDATE Users 
SET Role = 2 
WHERE Email = 'admin@kasserpro.com';

-- Verify
SELECT Id, Name, Email, Role FROM Users WHERE Email = 'admin@kasserpro.com';
